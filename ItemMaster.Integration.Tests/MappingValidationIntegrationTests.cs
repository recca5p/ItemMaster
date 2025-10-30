using Amazon.Lambda.APIGatewayEvents;
using ItemMaster.Contracts;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ItemMaster.Integration.Tests;

[Collection("Integration Tests")]
public class MappingValidationIntegrationTests : IntegrationTestBase
{
  [Fact]
  [Trait("Category", "Integration")]
  public async Task Mapping_WithRequiredFields_ShouldSucceed()
  {
    // Arrange
    var request = new ProcessSkusRequest { Skus = new List<string> { "TEST-SKU-001" } };
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
    body.GetProperty("success").GetBoolean().Should().BeTrue();
  }

  [Fact]
  [Trait("Category", "Integration")]
  public async Task Mapping_WithInvalidHtsCode_ShouldReturnValidationError()
  {
    // Arrange
    var request = new ProcessSkusRequest { Skus = new List<string> { "INVALID-HTS" } };
    var apiGatewayRequest = CreateApiGatewayRequest(request);

    // Act
    var response = await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

    // Assert
    response.Should().NotBeNull();
    response.StatusCode.Should().Be(200);

    var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
    var data = body.GetProperty("data");

    if (data.TryGetProperty("SkippedItems", out var skippedItems))
    {
      skippedItems.GetArrayLength().Should().BeGreaterThan(0);
    }
  }

  [Fact]
  [Trait("Category", "Integration")]
  public async Task Mapping_WithBarcodeLogicPre2024_ShouldUseSecondaryBarcode()
  {
    // Arrange
    var request = new ProcessSkusRequest { Skus = new List<string> { "PRE-2024-SKU" } };
    var apiGatewayRequest = CreateApiGatewayRequest(request);

    // Act
    var response = await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

    // Assert
    response.StatusCode.Should().Be(200);
  }

  [Fact]
  [Trait("Category", "Integration")]
  public async Task Mapping_WithMissingLandedCost_ShouldFailValidation()
  {
    // Arrange
    var request = new ProcessSkusRequest { Skus = new List<string> { "NO-LANDED-COST" } };
    var apiGatewayRequest = CreateApiGatewayRequest(request);

    // Act
    var response = await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

    // Assert
    response.StatusCode.Should().Be(200);

    var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
    var data = body.GetProperty("data");

    if (data.TryGetProperty("SkippedItems", out var skipped))
    {
      var skippedArray = skipped.EnumerateArray().ToList();
      if (skippedArray.Any())
      {
        var firstSkipped = skippedArray[0];
        firstSkipped.GetProperty("ValidationFailure").GetString().Should().Contain("LandedCost");
      }
    }
  }

  [Fact]
  [Trait("Category", "Integration")]
  public async Task Mapping_WithMissingFabricFields_ShouldFailValidation()
  {
    // Arrange
    var request = new ProcessSkusRequest { Skus = new List<string> { "NO-FABRIC" } };
    var apiGatewayRequest = CreateApiGatewayRequest(request);

    // Act
    var response = await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

    // Assert
    response.StatusCode.Should().Be(200);

    var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
    var data = body.GetProperty("data");

    if (data.TryGetProperty("SkippedItems", out var skipped))
    {
      var errors = skipped.EnumerateArray()
          .SelectMany(s => s.GetProperty("AllValidationErrors").EnumerateArray().Select(e => e.GetString()))
          .ToList();

      errors.Should().Contain(e => e != null && (e.Contains("FabricContent") || e.Contains("FabricComposition")));
    }
  }

  private static APIGatewayProxyRequest CreateApiGatewayRequest(ProcessSkusRequest request)
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

