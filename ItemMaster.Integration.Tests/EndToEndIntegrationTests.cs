using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.SQS.Model;
using FluentAssertions;
using ItemMaster.Contracts;
using ItemMaster.Shared;
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
    var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
    var data = body.GetProperty("data");
    data.GetProperty("ItemsProcessed").GetInt32().Should().Be(0);
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
    return new
    {
      source = "aws.events",
      "detail-type" = "Scheduled Event",
      detail = new { message = "Process latest items" }
    };
  }
}

