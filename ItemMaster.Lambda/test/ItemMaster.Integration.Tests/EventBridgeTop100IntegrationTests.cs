using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ItemMaster.Integration.Tests;

[Collection("Integration Tests")]
public class EventBridgeTop100IntegrationTests : IntegrationTestBase
{
  [Fact]
  [Trait("Category", "Integration")]
  public async Task EventBridge_NoSkus_ShouldProcessTop100AndPublish()
  {
    // Arrange
    var evbEvent = JsonSerializer.Deserialize<object>(
        "{" +
        "\"source\":\"aws.events\"," +
        "\"detail\":{}}"
    )!;

    // Act
    var response = await Function.FunctionHandler(evbEvent, LambdaContext);

    // Assert
    response.StatusCode.Should().Be(200);
    response.Body.Should().NotBeNullOrEmpty();
    var body = JsonDocument.Parse(response.Body);
    var data = body.RootElement.GetProperty("data");
    var processed = data.GetProperty("itemsProcessed").GetInt32();
    var published = data.GetProperty("itemsPublished").GetInt32();
    processed.Should().BeGreaterThan(0);
    published.Should().BeGreaterThan(0);
    published.Should().BeLessOrEqualTo(processed);
  }
}


