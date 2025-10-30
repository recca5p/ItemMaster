using FluentAssertions;
using ItemMaster.Infrastructure.Observability;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Observability;

public class TraceRecordTests
{
    [Fact]
    public void TraceRecord_DefaultConstructor_ShouldInitializeWithDefaultValues()
    {
        var record = new TraceRecord();

        record.Type.Should().BeNull();
        record.Name.Should().BeNull();
        record.Namespace.Should().BeNull();
        record.Key.Should().BeNull();
        record.Value.Should().BeNull();
        record.Exception.Should().BeNull();
        record.Timestamp.Should().Be(default);
        record.TraceId.Should().BeNull();
    }

    [Theory]
    [InlineData("BeginSubsegment", "TestSegment")]
    [InlineData("EndSubsegment", "TestSegment")]
    [InlineData("Annotation", "test-key")]
    [InlineData("Metadata", "metadata-key")]
    public void TraceRecord_WithTypeAndName_ShouldSetCorrectly(string type, string name)
    {
        var record = new TraceRecord
        {
            Type = type,
            Name = name
        };

        record.Type.Should().Be(type);
        record.Name.Should().Be(name);
    }

    [Fact]
    public void TraceRecord_WithMetadata_ShouldSetCorrectly()
    {
        var record = new TraceRecord
        {
            Type = "Metadata",
            Namespace = "itemmaster",
            Key = "sku",
            Value = "TEST-001"
        };

        record.Type.Should().Be("Metadata");
        record.Namespace.Should().Be("itemmaster");
        record.Key.Should().Be("sku");
        record.Value.Should().Be("TEST-001");
    }

    [Fact]
    public void TraceRecord_WithAnnotation_ShouldSetCorrectly()
    {
        var record = new TraceRecord
        {
            Type = "Annotation",
            Key = "operation",
            Value = "ProcessItems"
        };

        record.Type.Should().Be("Annotation");
        record.Key.Should().Be("operation");
        record.Value.Should().Be("ProcessItems");
    }

    [Fact]
    public void TraceRecord_WithException_ShouldSetCorrectly()
    {
        var exception = new InvalidOperationException("Test error");

        var record = new TraceRecord
        {
            Type = "Exception",
            Exception = exception
        };

        record.Type.Should().Be("Exception");
        record.Exception.Should().Be(exception);
    }

    [Fact]
    public void TraceRecord_WithTimestamp_ShouldSetCorrectly()
    {
        var timestamp = DateTime.UtcNow;

        var record = new TraceRecord
        {
            Type = "Test",
            Timestamp = timestamp
        };

        record.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void TraceRecord_WithTraceId_ShouldSetCorrectly()
    {
        var traceId = "1234567890abcdef";

        var record = new TraceRecord
        {
            Type = "Test",
            TraceId = traceId
        };

        record.TraceId.Should().Be(traceId);
    }

    [Theory]
    [InlineData("string", "test")]
    [InlineData("number", 42)]
    [InlineData("boolean", true)]
    [InlineData("double", 3.14)]
    [InlineData("datetime", "2023-10-15T10:30:00Z")]
    public void TraceRecord_WithVariousValueTypes_ShouldHandleCorrectly(string type, object value)
    {
        var record = new TraceRecord
        {
            Type = "Annotation",
            Key = type,
            Value = value
        };

        record.Value.Should().Be(value);
    }

    [Fact]
    public void TraceRecord_CompleteRecord_ShouldWorkCorrectly()
    {
        var exception = new Exception("Test");
        var timestamp = DateTime.UtcNow;
        var traceId = "test-trace-id";

        var record = new TraceRecord
        {
            Type = "CompleteRecord",
            Name = "TestSegment",
            Namespace = "itemmaster",
            Key = "test-key",
            Value = "test-value",
            Exception = exception,
            Timestamp = timestamp,
            TraceId = traceId
        };

        record.Type.Should().Be("CompleteRecord");
        record.Name.Should().Be("TestSegment");
        record.Namespace.Should().Be("itemmaster");
        record.Key.Should().Be("test-key");
        record.Value.Should().Be("test-value");
        record.Exception.Should().Be(exception);
        record.Timestamp.Should().Be(timestamp);
        record.TraceId.Should().Be(traceId);
    }
}