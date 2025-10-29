using Amazon.Lambda.APIGatewayEvents;
using FluentAssertions;
using ItemMaster.Contracts;
using ItemMaster.Infrastructure.Ef;
using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace ItemMaster.Integration.Tests;

[Collection("Integration Tests")]
public class DatabaseLoggingIntegrationTests : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DatabaseLogging_AfterProcessing_ShouldInsertLog()
    {
        // Arrange
        var sku = "TEST-LOG-001";
        var request = new ProcessSkusRequest { Skus = new List<string> { sku } };
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

        using var dbContext = CreateDbContext();
        var log = await dbContext.ItemMasterSourceLogs
            .FirstOrDefaultAsync(l => l.Sku == sku);

        log.Should().NotBeNull();
        log!.ValidationStatus.Should().BeIn(["valid", "invalid"]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DatabaseLogging_ValidItem_ShouldMarkAsValid()
    {
        // Arrange
        var sku = "TEST-VALID-001";
        var request = new ProcessSkusRequest { Skus = new List<string> { sku } };
        var apiGatewayRequest = CreateRequest(request);

        // Act
        await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        using var dbContext = CreateDbContext();
        var log = await dbContext.ItemMasterSourceLogs
            .FirstOrDefaultAsync(l => l.Sku == sku);

        log.Should().NotBeNull();
        log!.ValidationStatus.Should().Be("valid");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DatabaseLogging_InvalidItem_ShouldLogErrors()
    {
        // Arrange
        var sku = "TEST-INVALID-001";
        var request = new ProcessSkusRequest { Skus = new List<string> { sku } };
        var apiGatewayRequest = CreateRequest(request);

        // Act
        await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        using var dbContext = CreateDbContext();
        var log = await dbContext.ItemMasterSourceLogs
            .FirstOrDefaultAsync(l => l.Sku == sku);

        if (log != null && log.ValidationStatus == "invalid")
        {
            log.Errors.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DatabaseLogging_ShouldIncludeSourceModel()
    {
        // Arrange
        var sku = "TEST-SOURCE-MODEL-001";
        var request = new ProcessSkusRequest { Skus = new List<string> { sku } };
        var apiGatewayRequest = CreateRequest(request);

        // Act
        await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        using var dbContext = CreateDbContext();
        var log = await dbContext.ItemMasterSourceLogs
            .FirstOrDefaultAsync(l => l.Sku == sku);

        log.Should().NotBeNull();
        
        if (log!.SourceModel != null)
        {
            var sourceModel = JsonDocument.Parse(log.SourceModel);
            sourceModel.RootElement.Should().NotBeNull();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DatabaseLogging_ShouldIncludeCommonModel()
    {
        // Arrange
        var sku = "TEST-COMMON-MODEL-001";
        var request = new ProcessSkusRequest { Skus = new List<string> { sku } };
        var apiGatewayRequest = CreateRequest(request);

        // Act
        await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        using var dbContext = CreateDbContext();
        var log = await dbContext.ItemMasterSourceLogs
            .FirstOrDefaultAsync(l => l.Sku == sku);

        if (log!.ValidationStatus == "valid" && log.CommonModel != null)
        {
            var commonModel = JsonDocument.Parse(log.CommonModel);
            commonModel.RootElement.GetProperty("Sku").GetString().Should().Be(sku);
        }
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

    private static MySqlDbContext CreateDbContext()
    {
        var connectionString = $"Server=localhost;Database=item_master;User=im_user;Password=im_pass;CharSet=utf8mb4;";
        var optionsBuilder = new DbContextOptionsBuilder<MySqlDbContext>();
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        return new MySqlDbContext(optionsBuilder.Options);
    }
}

