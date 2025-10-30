using System.Text.Json;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Lambda;

public class RequestSourceDetector : IRequestSourceDetector
{
    private readonly ILogger<RequestSourceDetector> _logger;

    public RequestSourceDetector(ILogger<RequestSourceDetector> logger)
    {
        _logger = logger;
    }

    public RequestSource DetectSource(object input)
    {
        try
        {
            string inputJson;
            try
            {
                inputJson = input is string str ? str : JsonSerializer.Serialize(input);
            }
            catch
            {
                inputJson = "{}";
            }

            if (string.IsNullOrWhiteSpace(inputJson)) return RequestSource.CicdHealthCheck;

            var trimmed = inputJson.Trim();
            if (trimmed == "{}" || trimmed == "null") return RequestSource.CicdHealthCheck;

            var doc = JsonDocument.Parse(inputJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("source", out var source))
            {
                var sourceStr = source.GetString()?.ToLowerInvariant() ?? "";
                if (sourceStr.StartsWith("aws.") || sourceStr.Contains("eventbridge")) return RequestSource.EventBridge;

                if (root.TryGetProperty("detail-type", out _)) return RequestSource.EventBridge;
            }

            if (root.TryGetProperty("requestContext", out var requestContext))
            {
                var hasRequestId = requestContext.TryGetProperty("requestId", out _);
                var hasStage = requestContext.TryGetProperty("stage", out _);

                if (hasRequestId && hasStage) return RequestSource.ApiGateway;
            }

            return RequestSource.Lambda;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception during request source detection, defaulting to Lambda");
            return RequestSource.Lambda;
        }
    }
}