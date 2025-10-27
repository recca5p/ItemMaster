using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using ItemMaster.Contracts;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Lambda.Services;

public interface IRequestProcessingService
{
    ProcessSkusRequest ParseRequest(object input, RequestSource requestSource, string traceId);
}

public class RequestProcessingService : IRequestProcessingService
{
    private readonly ILogger<RequestProcessingService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RequestProcessingService(ILogger<RequestProcessingService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public ProcessSkusRequest ParseRequest(object input, RequestSource requestSource, string traceId)
    {
        try
        {
            var inputJson = ConvertInputToJson(input);
            
            return requestSource switch
            {
                RequestSource.EventBridge => ParseEventBridgeRequest(inputJson, traceId),
                RequestSource.ApiGateway => ParseApiGatewayRequest(inputJson, traceId),
                _ => ParseDirectInvocationRequest(inputJson, traceId)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse request, using default | RequestSource: {RequestSource} | TraceId: {TraceId}", 
                requestSource, traceId);
            return new ProcessSkusRequest();
        }
    }

    private string ConvertInputToJson(object input)
    {
        try
        {
            return input switch
            {
                string str => str,
                JsonDocument jsonDoc => jsonDoc.RootElement.GetRawText(),
                JsonElement jsonElem => jsonElem.GetRawText(),
                _ => JsonSerializer.Serialize(input, _jsonOptions)
            };
        }
        catch
        {
            return "{}";
        }
    }

    private ProcessSkusRequest ParseEventBridgeRequest(string inputJson, string traceId)
    {
        try
        {
            var eventDoc = JsonDocument.Parse(inputJson);
            if (eventDoc.RootElement.TryGetProperty("detail", out var detail))
            {
                return JsonSerializer.Deserialize<ProcessSkusRequest>(detail.GetRawText(), _jsonOptions) 
                       ?? new ProcessSkusRequest();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse EventBridge detail | TraceId: {TraceId}", traceId);
        }

        return new ProcessSkusRequest();
    }

    private ProcessSkusRequest ParseApiGatewayRequest(string inputJson, string traceId)
    {
        try
        {
            var apiGwRequest = JsonSerializer.Deserialize<APIGatewayProxyRequest>(inputJson, _jsonOptions);
            var bodyRaw = apiGwRequest?.Body;

            if (!string.IsNullOrWhiteSpace(bodyRaw))
            {
                if (apiGwRequest?.IsBase64Encoded == true)
                {
                    bodyRaw = DecodeBase64Body(bodyRaw, traceId);
                }

                return JsonSerializer.Deserialize<ProcessSkusRequest>(bodyRaw, _jsonOptions) 
                       ?? new ProcessSkusRequest();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApiGatewayRequestParseFailure | TraceId: {TraceId}", traceId);
        }

        return new ProcessSkusRequest();
    }

    private ProcessSkusRequest ParseDirectInvocationRequest(string inputJson, string traceId)
    {
        try
        {
            return JsonSerializer.Deserialize<ProcessSkusRequest>(inputJson, _jsonOptions) 
                   ?? new ProcessSkusRequest();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DirectInvocationParseFailure | TraceId: {TraceId}", traceId);
            return new ProcessSkusRequest();
        }
    }

    private string DecodeBase64Body(string bodyRaw, string traceId)
    {
        try
        {
            var bytes = Convert.FromBase64String(bodyRaw);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Base64DecodeFailure | TraceId: {TraceId}", traceId);
            throw new InvalidOperationException("Failed to decode base64 request body", ex);
        }
    }
}
