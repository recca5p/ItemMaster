using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using FluentAssertions;
using ItemMaster.Contracts;
using Xunit;

namespace ItemMaster.Integration.Tests;

public class EndToEndIntegrationTests : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task EndToEnd_HealthCheck_ShouldReturnSuccess()
    {
        // Arrange
        var input = "{}";

        // Act
        var response = await Function.FunctionHandler(input, LambdaContext);

        // Assert
        response.StatusCode.Should().Be(200);
        var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
        body.GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EndToEnd_ApiGatewayRequest_ShouldProcessRequest()
    {
        // Arrange
        var skus = new[] { "TEST-001", "TEST-002" };
        var apiGatewayRequest = CreateApiGatewayRequest(skus);

        // Act
        var response = await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().BeInRange(200, 299);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EndToEnd_EventBridgeRequest_ShouldProcessLatestItems()
    {
        // Arrange
        var eventBridgeRequest = CreateEventBridgeRequest();

        // Act
        var response = await Function.FunctionHandler(eventBridgeRequest, LambdaContext);

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().BeInRange(200, 299);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EndToEnd_EmptySkusList_ShouldReturnEmptyResponse()
    {
        // Arrange
        var request = CreateApiGatewayRequest(Array.Empty<string>());

        // Act
        var response = await Function.FunctionHandler(request, LambdaContext);

        // Assert
        response.StatusCode.Should().Be(200);
        response.Body.Should().NotBeNullOrEmpty();
        var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
        body.TryGetProperty("data", out _).Should().BeTrue();
        body.TryGetProperty("success", out _).Should().BeTrue();
        var data = body.GetProperty("data");
        data.ValueKind.Should().Be(JsonValueKind.Object);

        var hasItemsProcessed = data.TryGetProperty("itemsProcessed", out var itemsProcessedElement) ||
                                data.TryGetProperty("ItemsProcessed", out itemsProcessedElement);
        var hasItemsPublished = data.TryGetProperty("itemsPublished", out var itemsPublishedElement) ||
                                data.TryGetProperty("ItemsPublished", out itemsPublishedElement);

        if (hasItemsProcessed)
            itemsProcessedElement.GetInt32().Should().BeGreaterThanOrEqualTo(0);
        else if (hasItemsPublished)
            itemsPublishedElement.GetInt32().Should().BeGreaterThanOrEqualTo(0);
        else
            data.EnumerateObject().Any().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EndToEnd_InvalidRequest_ShouldReturnError()
    {
        // Arrange
        var invalidInput = "invalid-json-{";

        // Act
        var response = await Function.FunctionHandler(invalidInput, LambdaContext);

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().BeGreaterOrEqualTo(400);
    }

    private static APIGatewayProxyRequest CreateApiGatewayRequest(string[] skus)
    {
        var request = new ProcessSkusRequest { Skus = skus.ToList() };
        var jsonBody = JsonSerializer.Serialize(request);

        return new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Path = "/process-skus",
            Body = jsonBody,
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                RequestId = Guid.NewGuid().ToString(),
                Stage = "test"
            }
        };
    }

    private static object CreateEventBridgeRequest()
    {
        return new Dictionary<string, object>
        {
            ["source"] = "aws.events",
            ["detail-type"] = "Scheduled Event",
            ["detail"] = new Dictionary<string, object> { ["message"] = "Process latest items" }
        };
    }
}