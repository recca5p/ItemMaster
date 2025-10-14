using Amazon.Lambda.APIGatewayEvents;
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

    public RequestSource DetectSource(APIGatewayProxyRequest request)
    {
        if (IsHealthCheckRequest(request))
        {
            _logger.LogDebug("Detected health check request");
            return RequestSource.CicdHealthCheck;
        }

        if (IsEventBridgeRequest(request))
        {
            _logger.LogDebug("Detected EventBridge request");
            return RequestSource.EventBridge;
        }

        if (IsApiGatewayRequest(request))
        {
            _logger.LogDebug("Detected API Gateway request");
            return RequestSource.ApiGateway;
        }

        _logger.LogDebug("Request source unknown");
        return RequestSource.Unknown;
    }

    private static bool IsHealthCheckRequest(APIGatewayProxyRequest request)
    {
        var userAgent = request.Headers?.TryGetValue("User-Agent", out var ua) == true ? ua?.ToLowerInvariant() : null;

        if (userAgent != null && (
                userAgent.Contains("github-actions") ||
                userAgent.Contains("curl") ||
                userAgent.Contains("wget") ||
                userAgent.Contains("healthcheck") ||
                userAgent.Contains("monitoring")))
            return true;
        return false;
    }

    private static bool IsEventBridgeRequest(APIGatewayProxyRequest request)
    {
        var headers = request.Headers ?? new Dictionary<string, string>();

        if (headers.ContainsKey("X-Amz-Source") ||
            headers.ContainsKey("X-EventBridge-Source"))
            return true;

        var requestContext = request.RequestContext;
        if (requestContext?.Identity?.UserAgent?.Contains("EventBridge") == true) return true;

        return false;
    }

    private static bool IsApiGatewayRequest(APIGatewayProxyRequest request)
    {
        return request.RequestContext != null &&
               !string.IsNullOrWhiteSpace(request.RequestContext.RequestId);
    }
}