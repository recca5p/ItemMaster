using FluentAssertions;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Observability;

public class ObservabilityServiceTests
{
  private readonly Mock<ILogger<ObservabilityService>> _mockLogger;
  private readonly Mock<IMetricsService> _mockMetricsService;
  private readonly Mock<ITracingService> _mockTracingService;
  private readonly ObservabilityService _observabilityService;

  public ObservabilityServiceTests()
  {
    _mockLogger = new Mock<ILogger<ObservabilityService>>();
    _mockMetricsService = new Mock<IMetricsService>();
    _mockTracingService = new Mock<ITracingService>();

    _mockTracingService.Setup(x => x.GetCurrentTraceId()).Returns("test-trace-id");

    _observabilityService = new ObservabilityService(
        _mockLogger.Object,
        _mockMetricsService.Object,
        _mockTracingService.Object);
  }

  [Fact]
  public async Task ExecuteWithObservabilityAsync_WithSuccessfulOperation_ShouldReturnResult()
  {
    // Arrange
    var expectedResult = "test-result";
    Func<Task<string>> operation = async () =>
    {
      await Task.Delay(10);
      return expectedResult;
    };

    // Act
    var result = await _observabilityService.ExecuteWithObservabilityAsync(
        "TestOperation",
        RequestSource.ApiGateway,
        operation);

    // Assert
    result.Should().Be(expectedResult);
  }

  [Fact]
  public async Task ExecuteWithObservabilityAsync_WithSuccessfulOperation_ShouldCallTracingMethods()
  {
    // Arrange
    Func<Task<int>> operation = async () =>
    {
      await Task.Delay(10);
      return 42;
    };

    // Act
    await _observabilityService.ExecuteWithObservabilityAsync(
        "TestOperation",
        RequestSource.Sqs,
        operation);

    // Assert
    _mockTracingService.Verify(x => x.AddAnnotation("operation", "TestOperation"), Times.Once);
    _mockTracingService.Verify(x => x.AddAnnotation("requestSource", RequestSource.Sqs.ToString()), Times.Once);
    _mockTracingService.Verify(x => x.AddAnnotation("traceId", It.IsAny<string>()), Times.Once);
    _mockTracingService.Verify(x => x.BeginSubsegment("TestOperation"), Times.Once);
    _mockTracingService.Verify(x => x.AddAnnotation("success", true), Times.Once);
  }

  [Fact]
  public async Task ExecuteWithObservabilityAsync_WithFailedOperation_ShouldThrowAndLogError()
  {
    // Arrange
    var exception = new InvalidOperationException("Test error");
    Func<Task<string>> operation = async () =>
    {
      await Task.Delay(10);
      throw exception;
    };

    // Act
    var act = async () => await _observabilityService.ExecuteWithObservabilityAsync(
        "TestOperation",
        RequestSource.ApiGateway,
        operation);

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>();
    _mockTracingService.Verify(x => x.AddAnnotation("success", false), Times.Once);
    _mockTracingService.Verify(x => x.RecordException(exception), Times.Once);
  }

  [Fact]
  public async Task ExecuteWithObservabilityAsync_WithMetadata_ShouldPassMetadataToTracing()
  {
    // Arrange
    var metadata = new Dictionary<string, object>
        {
            { "sku", "TEST-001" },
            { "count", 10 }
        };
    Func<Task<int>> operation = async () =>
    {
      await Task.Delay(10);
      return 1;
    };

    // Act
    await _observabilityService.ExecuteWithObservabilityAsync(
        "TestOperation",
        RequestSource.ApiGateway,
        operation,
        metadata);

    // Assert
    _mockTracingService.Verify(x => x.AddMetadata("itemmaster", "sku", "TEST-001"), Times.Once);
    _mockTracingService.Verify(x => x.AddMetadata("itemmaster", "count", 10), Times.Once);
  }

  [Fact]
  public async Task ExecuteWithObservabilityAsync_ShouldRecordProcessingMetrics()
  {
    // Arrange
    Func<Task<string>> operation = async () =>
    {
      await Task.Delay(50);
      return "result";
    };

    // Act
    await _observabilityService.ExecuteWithObservabilityAsync(
        "TestOperation",
        RequestSource.Sqs,
        operation);

    // Assert
    _mockMetricsService.Verify(x => x.RecordProcessingMetricAsync(
        "TestOperation",
        true,
        RequestSource.Sqs,
        null,
        It.IsAny<TimeSpan?>(),
        It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task ExecuteWithObservabilityAsync_OnFailure_ShouldRecordFailedMetrics()
  {
    // Arrange
    Func<Task<string>> operation = async () =>
    {
      await Task.Delay(10);
      throw new Exception("Test error");
    };

    // Act
    var act = async () => await _observabilityService.ExecuteWithObservabilityAsync(
        "TestOperation",
        RequestSource.ApiGateway,
        operation);

    // Assert
    await act.Should().ThrowAsync<Exception>();
    _mockMetricsService.Verify(x => x.RecordProcessingMetricAsync(
        "TestOperation",
        false,
        RequestSource.ApiGateway,
        null,
        It.IsAny<TimeSpan?>(),
        It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public void GetCurrentTraceId_ShouldReturnTraceIdFromTracingService()
  {
    // Act
    var traceId = _observabilityService.GetCurrentTraceId();

    // Assert
    traceId.Should().Be("test-trace-id");
    _mockTracingService.Verify(x => x.GetCurrentTraceId(), Times.Once);
  }

  [Fact]
  public async Task RecordMetricAsync_ShouldCallMetricsService()
  {
    // Arrange
    var dimensions = new Dictionary<string, string> { { "env", "test" } };

    // Act
    await _observabilityService.RecordMetricAsync("TestMetric", 42.0, dimensions);

    // Assert
    _mockMetricsService.Verify(x => x.RecordCustomMetricAsync(
        "TestMetric",
        42.0,
        "Count",
        dimensions,
        It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public void LogWithTraceId_ShouldLogWithEnrichedMessage()
  {
    // Act
    _observabilityService.LogWithTraceId(LogLevel.Information, "Test message {Arg}", "value");

    // Assert
    _mockLogger.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Test message")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
  }

  [Fact]
  public async Task ExecuteWithObservabilityAsync_WithCancellation_ShouldHandleCorrectly()
  {
    // Arrange
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    Func<Task<string>> operation = async () =>
    {
      await Task.Delay(10, cts.Token);
      return "result";
    };

    // Act
    var act = async () => await _observabilityService.ExecuteWithObservabilityAsync(
        "TestOperation",
        RequestSource.ApiGateway,
        operation,
        null,
        cts.Token);

    // Assert
    await act.Should().ThrowAsync<OperationCanceledException>();
  }
}

