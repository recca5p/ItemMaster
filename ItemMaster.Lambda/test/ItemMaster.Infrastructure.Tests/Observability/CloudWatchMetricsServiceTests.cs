using Amazon.CloudWatch;
using FluentAssertions;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Observability;

public class CloudWatchMetricsServiceTests
{
    private readonly CloudWatchMetricsService _metricsService;
    private readonly Mock<IAmazonCloudWatch> _mockCloudWatch;

    public CloudWatchMetricsServiceTests()
    {
        _mockCloudWatch = new Mock<IAmazonCloudWatch>();
        _metricsService = new CloudWatchMetricsService(_mockCloudWatch.Object);
    }

    public static IEnumerable<object[]> GetProcessingMetricTestData()
    {
        // Test case 1: Successful processing metric
        yield return new object[]
        {
            "ProcessItems",
            true,
            RequestSource.ApiGateway,
            10,
            TimeSpan.FromMilliseconds(500),
            "Successful processing with all parameters"
        };

        // Test case 2: Failed processing metric
        yield return new object[]
        {
            "ProcessItems",
            false,
            RequestSource.Sqs,
            5,
            TimeSpan.FromMilliseconds(1000),
            "Failed processing with all parameters"
        };

        // Test case 3: Processing metric with null item count
        yield return new object[]
        {
            "ValidateItems",
            true,
            RequestSource.ApiGateway,
            null,
            TimeSpan.FromMilliseconds(200),
            "Processing metric with null item count"
        };

        // Test case 4: Processing metric with null duration
        yield return new object[]
        {
            "PublishItems",
            false,
            RequestSource.Sqs,
            20,
            null,
            "Processing metric with null duration"
        };
    }

    [Theory]
    [MemberData(nameof(GetProcessingMetricTestData))]
    public async Task RecordProcessingMetricAsync_WithVariousParameters_ShouldCompleteSuccessfully(
        string operation,
        bool success,
        RequestSource requestSource,
        int? itemCount,
        TimeSpan? duration,
        string scenario)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var act = async () => await _metricsService.RecordProcessingMetricAsync(
            operation, success, requestSource, itemCount, duration, cancellationToken);

        // Assert
        await act.Should().NotThrowAsync(scenario);
    }

    public static IEnumerable<object[]> GetCustomMetricTestData()
    {
        // Test case 1: Basic custom metric
        yield return new object[]
        {
            "ItemsProcessed",
            100.0,
            "Count",
            new Dictionary<string, string> { { "Region", "us-east-1" }, { "Environment", "prod" } },
            "Basic custom metric with dimensions"
        };

        // Test case 2: Custom metric with different unit
        yield return new object[]
        {
            "ProcessingDuration",
            1500.5,
            "Milliseconds",
            new Dictionary<string, string> { { "Operation", "Transform" } },
            "Custom metric with milliseconds unit"
        };

        // Test case 3: Custom metric with null dimensions
        yield return new object[]
        {
            "ErrorCount",
            5.0,
            "Count",
            null,
            "Custom metric with null dimensions"
        };
    }

    [Theory]
    [MemberData(nameof(GetCustomMetricTestData))]
    public async Task RecordCustomMetricAsync_WithVariousParameters_ShouldCompleteSuccessfully(
        string metricName,
        double value,
        string unit,
        Dictionary<string, string>? dimensions,
        string scenario)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var act = async () => await _metricsService.RecordCustomMetricAsync(
            metricName, value, unit, dimensions, cancellationToken);

        // Assert
        await act.Should().NotThrowAsync(scenario);
    }

    [Fact]
    public async Task RecordCustomMetricAsync_WithDefaultUnit_ShouldCompleteSuccessfully()
    {
        // Arrange
        var metricName = "TestMetric";
        var value = 42.0;

        // Act
        var act = async () => await _metricsService.RecordCustomMetricAsync(metricName, value);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordProcessingMetricAsync_WithCancellation_ShouldHandleGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _metricsService.RecordProcessingMetricAsync(
            "TestOperation", true, RequestSource.ApiGateway, cancellationToken: cts.Token);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordCustomMetricAsync_WithCancellation_ShouldHandleGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _metricsService.RecordCustomMetricAsync(
            "TestMetric", 42.0, cancellationToken: cts.Token);

        await act.Should().NotThrowAsync();
    }
}