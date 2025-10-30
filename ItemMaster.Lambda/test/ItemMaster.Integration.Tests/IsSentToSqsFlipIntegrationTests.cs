using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using FluentAssertions;
using ItemMaster.Infrastructure.Ef;
using ItemMaster.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ItemMaster.Integration.Tests;

[Collection("Integration Tests")]
public class IsSentToSqsFlipIntegrationTests : IntegrationTestBase
{
  [Fact]
  [Trait("Category", "Integration")]
  public async Task AfterSuccessfulPublish_LogShouldFlipIsSentToSqs()
  {
    // Arrange
    var sku = "FLIP-001";
    var request = new ProcessSkusRequest { Skus = new List<string> { sku } };
    var apiRequest = new APIGatewayProxyRequest
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
    var response = await Function.FunctionHandler(apiRequest, LambdaContext);
    response.StatusCode.Should().Be(200);

    await Task.Delay(2000);

    // Assert (DB) with simple retry
    var conn = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
    var db = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "item_master";
    var user = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "im_user";
    var pass = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "im_pass";
    var cs = $"Server={conn};Database={db};User={user};Password={pass};CharSet=utf8mb4;";

    var opts = new DbContextOptionsBuilder<MySqlDbContext>()
        .UseMySql(cs, new MySqlServerVersion(new Version(8, 0, 35)))
        .Options;

    await using var ctx = new MySqlDbContext(opts);
    ItemMaster.Shared.ItemMasterSourceLog? latest = null;
    for (var i = 0; i < 10; i++)
    {
      latest = await ctx.ItemMasterSourceLogs
          .Where(x => x.Sku == sku)
          .OrderByDescending(x => x.CreatedAt)
          .FirstOrDefaultAsync();

      if (latest != null && latest.IsSentToSqs) break;
      await Task.Delay(500);
    }

    latest.Should().NotBeNull();
    latest!.IsSentToSqs.Should().BeTrue();
  }
}


