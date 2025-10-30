using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using FluentAssertions;
using ItemMaster.Contracts;
using Xunit;

namespace ItemMaster.Integration.Tests;

public class SkuProcessingIntegrationTests : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessSkus_WithValidSkus_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "SKU-001", "SKU-002" } };
        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(request),
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                RequestId = Guid.NewGuid().ToString(),
                Stage = "test"
            }
        };

        // Act
        var response = await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().Be(200);

        var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessSkus_WithEmptySkus_ShouldFetchLatestItems()
    {
        // Arrange
        var request = new ProcessSkusRequest();
        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(request),
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                RequestId = Guid.NewGuid().ToString(),
                Stage = "test"
            }
        };

        // Act
        var response = await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        response.StatusCode.Should().Be(200);

        var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
        var data = body.GetProperty("data");
        data.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessSkus_MultipleRequests_ShouldHandleConcurrently()
    {
        // Arrange
        var tasks = new List<Task<APIGatewayProxyResponse>>();

        // Act
        for (var i = 0; i < 3; i++)
        {
            var request = new ProcessSkusRequest { Skus = new List<string> { $"SKU-{i:D3}" } };
            var apiGatewayRequest = CreateRequest(request);
            tasks.Add(Function.FunctionHandler(apiGatewayRequest, LambdaContext));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(3);
        responses.All(r => r.StatusCode >= 200 && r.StatusCode < 300).Should().BeTrue();
    }

    private static APIGatewayProxyRequest CreateRequest(ProcessSkusRequest request)
    {
        return new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(request),
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                RequestId = Guid.NewGuid().ToString(),
                Stage = "test"
            }
        };
    }
}