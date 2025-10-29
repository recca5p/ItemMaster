using FluentAssertions;
using ItemMaster.Lambda;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace ItemMaster.Lambda.Tests;

public class RequestSourceDetectorTests
{
    private readonly Mock<ILogger<RequestSourceDetector>> _mockLogger;
    private readonly RequestSourceDetector _detector;

    public RequestSourceDetectorTests()
    {
        _mockLogger = new Mock<ILogger<RequestSourceDetector>>();
        _detector = new RequestSourceDetector(_mockLogger.Object);
    }

    [Fact]
    public void DetectSource_WithNullInput_ShouldReturnCicdHealthCheck()
    {
        // Arrange
        object? input = null;

        // Act
        var result = _detector.DetectSource(input!);

        // Assert
        result.Should().Be(RequestSource.CicdHealthCheck);
    }

    [Fact]
    public void DetectSource_WithEmptyString_ShouldReturnCicdHealthCheck()
    {
        // Arrange
        var input = "";

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.CicdHealthCheck);
    }

    [Fact]
    public void DetectSource_WithWhitespace_ShouldReturnCicdHealthCheck()
    {
        // Arrange
        var input = "   ";

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.CicdHealthCheck);
    }

    [Fact]
    public void DetectSource_WithEmptyJsonObject_ShouldReturnCicdHealthCheck()
    {
        // Arrange
        var input = "{}";

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.CicdHealthCheck);
    }

    [Fact]
    public void DetectSource_WithNullJsonValue_ShouldReturnCicdHealthCheck()
    {
        // Arrange
        var input = "null";

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.CicdHealthCheck);
    }

    [Fact]
    public void DetectSource_WithEventBridgeEvent_ShouldReturnEventBridge()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new
        {
            source = "aws.sqs"
        });

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.EventBridge);
    }

    [Fact]
    public void DetectSource_WithEventBridgeContainingDetailType_ShouldReturnEventBridge()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["source"] = "custom.source",
            ["detail-type"] = "TestEvent"
        });

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.EventBridge);
    }

    [Fact]
    public void DetectSource_WithApiGatewayRequest_ShouldReturnApiGateway()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new
        {
            requestContext = new
            {
                requestId = "test-request-id",
                stage = "prod"
            }
        });

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.ApiGateway);
    }

    [Fact]
    public void DetectSource_WithUnexpectedData_ShouldReturnLambda()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { someProperty = "value" });

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.Lambda);
    }

    [Fact]
    public void DetectSource_WithInvalidJson_ShouldReturnLambda()
    {
        // Arrange
        var input = "invalid json {";

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.Lambda);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("aws.s3")]
    [InlineData("aws.sns")]
    [InlineData("aws.eventbridge")]
    [InlineData("custom.eventbridge")]
    public void DetectSource_WithAwsSource_ShouldReturnEventBridge(string source)
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { source });

        // Act
        var result = _detector.DetectSource(input);

        // Assert
        result.Should().Be(RequestSource.EventBridge);
    }
}

