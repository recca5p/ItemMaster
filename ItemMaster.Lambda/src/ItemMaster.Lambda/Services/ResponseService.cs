using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using ItemMaster.Contracts;
using ItemMaster.Shared;

namespace ItemMaster.Lambda.Services;

public interface IResponseService
{
    APIGatewayProxyResponse CreateSuccessResponse<T>(T data, string traceId);
    APIGatewayProxyResponse CreateErrorResponse(string error, string traceId, int statusCode = 500);
    APIGatewayProxyResponse CreateHealthCheckResponse();
}

public class ResponseService : IResponseService
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ResponseService()
    {
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }
    public APIGatewayProxyResponse CreateSuccessResponse<T>(T data, string traceId)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new
            {
                success = true,
                data,
                traceId
            }, _jsonOptions),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }

    public APIGatewayProxyResponse CreateErrorResponse(string error, string traceId, int statusCode = 500)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Body = JsonSerializer.Serialize(new { error, traceId }, _jsonOptions),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }

    public APIGatewayProxyResponse CreateHealthCheckResponse()
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new
            {
                status = "healthy",
                message = "Lambda function is operational",
                timestamp = DateTime.UtcNow,
                source = "health_check"
            }, _jsonOptions),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }
}

