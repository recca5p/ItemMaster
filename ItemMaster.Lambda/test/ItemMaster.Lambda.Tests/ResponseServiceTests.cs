using Amazon.Lambda.APIGatewayEvents;
using FluentAssertions;
using ItemMaster.Lambda.Services;
using System.Text.Json;
using Xunit;

namespace ItemMaster.Lambda.Tests;

public class ResponseServiceTests
{
    private readonly ResponseService _service;

    public ResponseServiceTests()
    {
        _service = new ResponseService();
    }

    [Fact]
    public void CreateSuccessResponse_WithData_ShouldReturn200StatusCode()
    {
        // Arrange
        var data = new { message = "Test" };
        var traceId = "trace-123";

        // Act
        var result = _service.CreateSuccessResponse(data, traceId);

        // Assert
        result.StatusCode.Should().Be(200);
        result.Headers["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public void CreateSuccessResponse_WithData_ShouldIncludeSuccessFlag()
    {
        // Arrange
        var data = new { message = "Test" };
        var traceId = "trace-123";

        // Act
        var result = _service.CreateSuccessResponse(data, traceId);

        // Assert
        var body = JsonSerializer.Deserialize<JsonElement>(result.Body);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("traceId").GetString().Should().Be(traceId);
    }

    [Fact]
    public void CreateErrorResponse_WithError_ShouldReturn500StatusCode()
    {
        // Arrange
        var error = "Test error";
        var traceId = "trace-456";

        // Act
        var result = _service.CreateErrorResponse(error, traceId);

        // Assert
        result.StatusCode.Should().Be(500);
        result.Headers["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public void CreateErrorResponse_WithCustomStatusCode_ShouldUseProvidedCode()
    {
        // Arrange
        var error = "Not Found";
        var traceId = "trace-789";

        // Act
        var result = _service.CreateErrorResponse(error, traceId, 404);

        // Assert
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public void CreateErrorResponse_ShouldIncludeErrorAndTraceId()
    {
        // Arrange
        var error = "Test error";
        var traceId = "trace-error";

        // Act
        var result = _service.CreateErrorResponse(error, traceId);

        // Assert
        var body = JsonSerializer.Deserialize<JsonElement>(result.Body);
        body.GetProperty("error").GetString().Should().Be(error);
        body.GetProperty("traceId").GetString().Should().Be(traceId);
    }

    [Fact]
    public void CreateHealthCheckResponse_ShouldReturnHealthyStatus()
    {
        // Arrange & Act
        var result = _service.CreateHealthCheckResponse();

        // Assert
        result.StatusCode.Should().Be(200);
        result.Headers["Content-Type"].Should().Be("application/json");
        
        var body = JsonSerializer.Deserialize<JsonElement>(result.Body);
        body.GetProperty("status").GetString().Should().Be("healthy");
        body.GetProperty("message").GetString().Should().Be("Lambda function is operational");
        body.GetProperty("source").GetString().Should().Be("health_check");
    }

    [Fact]
    public void CreateHealthCheckResponse_ShouldIncludeTimestamp()
    {
        // Arrange & Act
        var result = _service.CreateHealthCheckResponse();

        // Assert
        var body = JsonSerializer.Deserialize<JsonElement>(result.Body);
        body.GetProperty("timestamp").ValueKind.Should().Be(System.Text.Json.JsonValueKind.String);
        var timestamp = body.GetProperty("timestamp").GetString();
        DateTime.TryParse(timestamp, out _).Should().BeTrue();
    }
}

