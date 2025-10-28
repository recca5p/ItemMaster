using FluentAssertions;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Xunit;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Observability;

public class MetricRecordTests
{
    public static IEnumerable<object[]> GetMetricRecordTestData()
    {
        // Test case 1: Processing metric record
        yield return new object[]
        {
            new MetricRecord
            {
                Operation = "ProcessItems",
                Success = true,
                RequestSource = RequestSource.ApiGateway,
                ItemCount = 10,
                Duration = TimeSpan.FromMilliseconds(500),
                Timestamp = DateTime.UtcNow
            },
            "Processing metric record"
        };

        // Test case 2: Custom metric record
        yield return new object[]
        {
            new MetricRecord
            {
                MetricName = "ItemsProcessed",
                Value = 100.0,
                Unit = "Count",
                Dimensions = new Dictionary<string, string> { { "Region", "us-east-1" } },
                Timestamp = DateTime.UtcNow
            },
            "Custom metric record"
        };

        // Test case 3: Mixed metric record with all properties
        yield return new object[]
        {
            new MetricRecord
            {
                Operation = "ValidateItems",
                Success = false,
                RequestSource = RequestSource.Sqs,
                ItemCount = 5,
                Duration = TimeSpan.FromMilliseconds(200),
                MetricName = "ValidationErrors",
                Value = 3.0,
                Unit = "Count",
                Dimensions = new Dictionary<string, string> { { "ErrorType", "Schema" } },
                Timestamp = DateTime.UtcNow
            },
            "Mixed metric record with all properties"
        };

        // Test case 4: Minimal metric record
        yield return new object[]
        {
            new MetricRecord
            {
                Success = true,
                RequestSource = RequestSource.ApiGateway,
                Value = 1.0,
                Timestamp = DateTime.UtcNow
            },
            "Minimal metric record"
        };
    }

    [Theory]
    [MemberData(nameof(GetMetricRecordTestData))]
    public void MetricRecord_WithDifferentProperties_ShouldInitializeCorrectly(
        MetricRecord expectedRecord,
        string scenario)
    {
        // Arrange & Act
        var actualRecord = new MetricRecord
        {
            Operation = expectedRecord.Operation,
            Success = expectedRecord.Success,
            RequestSource = expectedRecord.RequestSource,
            ItemCount = expectedRecord.ItemCount,
            Duration = expectedRecord.Duration,
            MetricName = expectedRecord.MetricName,
            Value = expectedRecord.Value,
            Unit = expectedRecord.Unit,
            Dimensions = expectedRecord.Dimensions,
            Timestamp = expectedRecord.Timestamp
        };

        // Assert
        actualRecord.Should().BeEquivalentTo(expectedRecord, scenario);
    }

    [Fact]
    public void MetricRecord_DefaultConstructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var record = new MetricRecord();

        // Assert
        record.Operation.Should().BeNull();
        record.Success.Should().BeFalse();
        record.RequestSource.Should().Be(default(RequestSource));
        record.ItemCount.Should().BeNull();
        record.Duration.Should().BeNull();
        record.MetricName.Should().BeNull();
        record.Value.Should().Be(0.0);
        record.Unit.Should().BeNull();
        record.Dimensions.Should().BeNull();
        record.Timestamp.Should().Be(default(DateTime));
    }

    [Theory]
    [InlineData("ProcessItems", true, RequestSource.ApiGateway, 10, 500)]
    [InlineData("ValidateItems", false, RequestSource.Sqs, 5, 200)]
    [InlineData("PublishItems", true, RequestSource.ApiGateway, 20, 1000)]
    public void MetricRecord_ProcessingProperties_ShouldBeSettableAndGettable(
        string operation,
        bool success,
        RequestSource requestSource,
        int itemCount,
        int durationMs)
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var timestamp = DateTime.UtcNow;

        // Act
        var record = new MetricRecord
        {
            Operation = operation,
            Success = success,
            RequestSource = requestSource,
            ItemCount = itemCount,
            Duration = duration,
            Timestamp = timestamp
        };

        // Assert
        record.Operation.Should().Be(operation);
        record.Success.Should().Be(success);
        record.RequestSource.Should().Be(requestSource);
        record.ItemCount.Should().Be(itemCount);
        record.Duration.Should().Be(duration);
        record.Timestamp.Should().Be(timestamp);
    }

    [Theory]
    [InlineData("ItemsProcessed", 100.0, "Count")]
    [InlineData("ProcessingDuration", 1500.5, "Milliseconds")]
    [InlineData("ErrorRate", 0.05, "Percent")]
    [InlineData("MemoryUsage", 85.7, "Bytes")]
    public void MetricRecord_CustomMetricProperties_ShouldBeSettableAndGettable(
        string metricName,
        double value,
        string unit)
    {
        // Arrange
        var dimensions = new Dictionary<string, string>
        {
            { "Region", "us-east-1" },
            { "Environment", "prod" }
        };
        var timestamp = DateTime.UtcNow;

        // Act
        var record = new MetricRecord
        {
            MetricName = metricName,
            Value = value,
            Unit = unit,
            Dimensions = dimensions,
            Timestamp = timestamp
        };

        // Assert
        record.MetricName.Should().Be(metricName);
        record.Value.Should().Be(value);
        record.Unit.Should().Be(unit);
        record.Dimensions.Should().BeEquivalentTo(dimensions);
        record.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void MetricRecord_WithNullDimensions_ShouldHandleCorrectly()
    {
        // Arrange & Act
        var record = new MetricRecord
        {
            MetricName = "TestMetric",
            Value = 42.0,
            Unit = "Count",
            Dimensions = null
        };

        // Assert
        record.Dimensions.Should().BeNull();
    }

    [Fact]
    public void MetricRecord_WithEmptyDimensions_ShouldHandleCorrectly()
    {
        // Arrange
        var emptyDimensions = new Dictionary<string, string>();

        // Act
        var record = new MetricRecord
        {
            MetricName = "TestMetric",
            Value = 42.0,
            Unit = "Count",
            Dimensions = emptyDimensions
        };

        // Assert
        record.Dimensions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void MetricRecord_WithComplexDimensions_ShouldHandleCorrectly()
    {
        // Arrange
        var complexDimensions = new Dictionary<string, string>
        {
            { "Region", "us-east-1" },
            { "Environment", "production" },
            { "Service", "ItemMaster" },
            { "Version", "1.0.0" },
            { "Instance", "i-1234567890abcdef0" }
        };

        // Act
        var record = new MetricRecord
        {
            MetricName = "ComplexMetric",
            Value = 123.45,
            Unit = "Custom",
            Dimensions = complexDimensions
        };

        // Assert
        record.Dimensions.Should().HaveCount(5);
        record.Dimensions.Should().ContainKeys("Region", "Environment", "Service", "Version", "Instance");
        record.Dimensions!["Region"].Should().Be("us-east-1");
        record.Dimensions!["Environment"].Should().Be("production");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(-5)]
    public void MetricRecord_WithDifferentItemCounts_ShouldHandleCorrectly(int? itemCount)
    {
        // Arrange & Act
        var record = new MetricRecord
        {
            Operation = "TestOperation",
            ItemCount = itemCount
        };

        // Assert
        record.ItemCount.Should().Be(itemCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(1500)]
    [InlineData(60000)]
    public void MetricRecord_WithDifferentDurations_ShouldHandleCorrectly(int? durationMs)
    {
        // Arrange
        TimeSpan? duration = durationMs.HasValue ? TimeSpan.FromMilliseconds(durationMs.Value) : null;

        // Act
        var record = new MetricRecord
        {
            Operation = "TestOperation",
            Duration = duration
        };

        // Assert
        record.Duration.Should().Be(duration);
    }

    [Theory]
    [InlineData(RequestSource.ApiGateway)]
    [InlineData(RequestSource.Sqs)]
    public void MetricRecord_WithDifferentRequestSources_ShouldHandleCorrectly(RequestSource requestSource)
    {
        // Arrange & Act
        var record = new MetricRecord
        {
            RequestSource = requestSource
        };

        // Assert
        record.RequestSource.Should().Be(requestSource);
    }
}
