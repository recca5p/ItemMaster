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

        _logger.LogDebug("Detected direct Lambda invocation");
        return RequestSource.Lambda;
    }

    private static bool IsHealthCheckRequest(APIGatewayProxyRequest request)
    {
        var hasMinimalBody = string.IsNullOrWhiteSpace(request.Body) ||
                             request.Body.Trim() == "{}" ||
                             request.Body.Trim() == "{}";

        var hasNoQueryParams = request.QueryStringParameters == null ||
                               request.QueryStringParameters.Count == 0;

        var hasMinimalPath = string.IsNullOrWhiteSpace(request.Path) ||
                            request.Path == "/" ||
                            request.Path == "/health";

        if (request.Headers != null && request.Headers.Count > 0)
        {
            var userAgent = request.Headers.TryGetValue("User-Agent", out var ua) ? ua?.ToLowerInvariant() : null;
            if (userAgent != null && (
                    userAgent.Contains("github-actions") ||
                    userAgent.Contains("curl") ||
                    userAgent.Contains("wget") ||
                    userAgent.Contains("healthcheck") ||
                    userAgent.Contains("monitoring") ||
                    userAgent.Contains("aws-cli")))
            {
                return true;
            }
        }

        if (hasMinimalBody && hasNoQueryParams && hasMinimalPath)
        {
            var hasRequestContext = request.RequestContext != null &&
                                   !string.IsNullOrWhiteSpace(request.RequestContext.RequestId);

            var hasEventBridgeHeaders = request.Headers != null && (
                request.Headers.ContainsKey("X-Amz-Source") ||
                request.Headers.ContainsKey("X-EventBridge-Source"));

            if (!hasRequestContext && !hasEventBridgeHeaders)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEventBridgeRequest(APIGatewayProxyRequest request)
    {
        var headers = request.Headers ?? new Dictionary<string, string>();

        if (headers.ContainsKey("X-Amz-Source") ||
            headers.ContainsKey("X-EventBridge-Source") ||
            headers.ContainsKey("X-Amz-Firehose-Source-Arn"))
        {
            return true;
        }

        var requestContext = request.RequestContext;
        if (requestContext?.Identity?.UserAgent?.Contains("EventBridge") == true ||
            requestContext?.Identity?.UserAgent?.Contains("Amazon-EventBridge") == true)
        {
            return true;
        }

        // Check if the source is in the body (for scheduled EventBridge events)
        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            var bodyLower = request.Body.ToLowerInvariant();
            if (bodyLower.Contains("\"source\":\"aws.events\"") ||
                bodyLower.Contains("\"source\":\"aws.scheduler\"") ||
                bodyLower.Contains("\"detail-type\"") ||
                bodyLower.Contains("\"eventbridge\""))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsApiGatewayRequest(APIGatewayProxyRequest request)
    {
        // API Gateway has a proper request context with requestId
        return request.RequestContext != null &&
               !string.IsNullOrWhiteSpace(request.RequestContext.RequestId) &&
               !string.IsNullOrWhiteSpace(request.RequestContext.Stage);
    }
}