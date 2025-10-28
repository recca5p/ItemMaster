using FluentAssertions;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Xunit;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Observability;

public class InMemoryMetricsServiceTests
{
    private readonly InMemoryMetricsService _metricsService;

    public InMemoryMetricsServiceTests()
    {
        _metricsService = new InMemoryMetricsService();
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

        // Test case 5: Processing metric with all nullable parameters null
        yield return new object[]
        {
            "InitializeService",
            true,
            RequestSource.ApiGateway,
            null,
            null,
            "Processing metric with all nullable parameters null"
        };
    }

    [Theory]
    [MemberData(nameof(GetProcessingMetricTestData))]
    public async Task RecordProcessingMetricAsync_WithVariousParameters_ShouldRecordCorrectly(
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
        await _metricsService.RecordProcessingMetricAsync(
            operation, success, requestSource, itemCount, duration, cancellationToken);

        // Assert
        var metrics = _metricsService.GetMetrics();
        metrics.Should().HaveCount(1, scenario);
        
        var metric = metrics.First();
        metric.Operation.Should().Be(operation, scenario);
        metric.Success.Should().Be(success, scenario);
        metric.RequestSource.Should().Be(requestSource, scenario);
        metric.ItemCount.Should().Be(itemCount, scenario);
        metric.Duration.Should().Be(duration, scenario);
        metric.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), scenario);
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

        // Test case 4: Custom metric with empty dimensions
        yield return new object[]
        {
            "MemoryUsage",
            85.7,
            "Percent",
            new Dictionary<string, string>(),
            "Custom metric with empty dimensions"
        };

        // Test case 5: Custom metric with default unit
        yield return new object[]
        {
            "ActiveConnections",
            25.0,
            "Count",
            new Dictionary<string, string> { { "Pool", "Primary" } },
            "Custom metric with default unit"
        };
    }

    [Theory]
    [MemberData(nameof(GetCustomMetricTestData))]
    public async Task RecordCustomMetricAsync_WithVariousParameters_ShouldRecordCorrectly(
        string metricName,
        double value,
        string unit,
        Dictionary<string, string>? dimensions,
        string scenario)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        await _metricsService.RecordCustomMetricAsync(metricName, value, unit, dimensions, cancellationToken);

        // Assert
        var metrics = _metricsService.GetMetrics();
        metrics.Should().HaveCount(1, scenario);
        
        var metric = metrics.First();
        metric.MetricName.Should().Be(metricName, scenario);
        metric.Value.Should().Be(value, scenario);
        metric.Unit.Should().Be(unit, scenario);
        metric.Dimensions.Should().BeEquivalentTo(dimensions, scenario);
        metric.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), scenario);
    }

    [Fact]
    public async Task RecordCustomMetricAsync_WithDefaultUnit_ShouldUseCountAsDefault()
    {
        // Arrange
        var metricName = "TestMetric";
        var value = 42.0;

        // Act
        await _metricsService.RecordCustomMetricAsync(metricName, value);

        // Assert
        var metrics = _metricsService.GetMetrics();
        metrics.Should().HaveCount(1);
        
        var metric = metrics.First();
        metric.Unit.Should().Be("Count");
    }

    [Fact]
    public async Task RecordMultipleMetrics_ShouldStoreAllMetrics()
    {
        // Arrange
        var metrics = new[]
        {
            ("ProcessItems", true, RequestSource.ApiGateway, 10, TimeSpan.FromMilliseconds(500)),
            ("ValidateItems", false, RequestSource.Sqs, 5, TimeSpan.FromMilliseconds(200)),
            ("PublishItems", true, RequestSource.ApiGateway, 15, TimeSpan.FromMilliseconds(300))
        };

        // Act
        foreach (var (operation, success, requestSource, itemCount, duration) in metrics)
        {
            await _metricsService.RecordProcessingMetricAsync(
                operation, success, requestSource, itemCount, duration);
        }

        // Assert
        var recordedMetrics = _metricsService.GetMetrics();
        recordedMetrics.Should().HaveCount(3);
        
        recordedMetrics.Should().Contain(m => m.Operation == "ProcessItems" && m.Success == true);
        recordedMetrics.Should().Contain(m => m.Operation == "ValidateItems" && m.Success == false);
        recordedMetrics.Should().Contain(m => m.Operation == "PublishItems" && m.Success == true);
    }

    [Fact]
    public async Task RecordMixedMetricTypes_ShouldStoreAllMetrics()
    {
        // Arrange & Act
        await _metricsService.RecordProcessingMetricAsync(
            "ProcessItems", true, RequestSource.ApiGateway, 10, TimeSpan.FromMilliseconds(500));
        
        await _metricsService.RecordCustomMetricAsync(
            "CustomCounter", 25.0, "Count", new Dictionary<string, string> { { "Type", "Test" } });

        // Assert
        var metrics = _metricsService.GetMetrics();
        metrics.Should().HaveCount(2);
        
        metrics.Should().Contain(m => m.Operation == "ProcessItems");
        metrics.Should().Contain(m => m.MetricName == "CustomCounter");
    }

    [Fact]
    public void Clear_ShouldRemoveAllMetrics()
    {
        // Arrange
        _metricsService.RecordProcessingMetricAsync(
            "TestOperation", true, RequestSource.ApiGateway, 5, TimeSpan.FromMilliseconds(100));
        
        _metricsService.RecordCustomMetricAsync("TestMetric", 10.0);

        // Act
        _metricsService.Clear();

        // Assert
        var metrics = _metricsService.GetMetrics();
        metrics.Should().BeEmpty();
    }

    [Fact]
    public void GetMetrics_ShouldReturnReadOnlyList()
    {
        // Arrange
        _metricsService.RecordProcessingMetricAsync(
            "TestOperation", true, RequestSource.ApiGateway, 5, TimeSpan.FromMilliseconds(100));

        // Act
        var metrics = _metricsService.GetMetrics();

        // Assert
        metrics.Should().BeAssignableTo<IReadOnlyList<MetricRecord>>();
        
        // Verify it's actually read-only by attempting to cast and modify
        Action attemptModification = () =>
        {
            if (metrics is List<MetricRecord> list)
            {
                list.Add(new MetricRecord());
            }
        };
        
        // This should not throw, but the original collection should remain unchanged
        attemptModification.Should().NotThrow();
        
        // Verify original collection is protected
        var metricsAfter = _metricsService.GetMetrics();
        metricsAfter.Should().HaveCount(1); // Should still be 1, not 2
    }

    [Fact]
    public async Task RecordProcessingMetricAsync_WithCancellation_ShouldCompleteSuccessfully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _metricsService.RecordProcessingMetricAsync(
            "TestOperation", true, RequestSource.ApiGateway, cancellationToken: cts.Token);
        
        await act.Should().NotThrowAsync();
        
        var metrics = _metricsService.GetMetrics();
        metrics.Should().HaveCount(1);
    }

    [Fact]
    public async Task RecordCustomMetricAsync_WithCancellation_ShouldCompleteSuccessfully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _metricsService.RecordCustomMetricAsync(
            "TestMetric", 42.0, cancellationToken: cts.Token);
        
        await act.Should().NotThrowAsync();
        
        var metrics = _metricsService.GetMetrics();
        metrics.Should().HaveCount(1);
    }
}
