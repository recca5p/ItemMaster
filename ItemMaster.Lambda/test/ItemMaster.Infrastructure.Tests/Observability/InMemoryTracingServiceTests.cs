using FluentAssertions;
using ItemMaster.Infrastructure.Observability;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Observability;

public class InMemoryTracingServiceTests
{
  [Fact]
  public void GetCurrentTraceId_ShouldReturnNonNullValue()
  {
    // Arrange
    var service = new InMemoryTracingService();

    // Act
    var traceId = service.GetCurrentTraceId();

    // Assert
    traceId.Should().NotBeNull();
    traceId.Should().HaveLength(16);
  }

  [Fact]
  public void BeginSubsegment_ShouldReturnDisposable()
  {
    // Arrange
    var service = new InMemoryTracingService();

    // Act
    var disposable = service.BeginSubsegment("test-segment");

    // Assert
    disposable.Should().NotBeNull();
    service.GetTraces().Should().HaveCount(1);
    service.GetTraces().First().Type.Should().Be("BeginSubsegment");
    service.GetTraces().First().Name.Should().Be("test-segment");
  }

  [Fact]
  public void BeginSubsegment_ShouldTrackTimestamp()
  {
    // Arrange
    var service = new InMemoryTracingService();
    var before = DateTime.UtcNow;

    // Act
    var disposable = service.BeginSubsegment("test");
    var after = DateTime.UtcNow;

    // Assert
    var record = service.GetTraces().First();
    record.Timestamp.Should().BeCloseTo(before, TimeSpan.FromSeconds(1));
    record.Timestamp.Should().BeBefore(after);
  }

  [Fact]
  public void BeginSubsegment_Dispose_ShouldCallEndSubsegment()
  {
    // Arrange
    var service = new InMemoryTracingService();

    // Act
    var disposable = service.BeginSubsegment("test-segment");
    disposable.Dispose();

    // Assert
    service.GetTraces().Should().HaveCount(2);
    service.GetTraces().First().Type.Should().Be("BeginSubsegment");
    service.GetTraces().Last().Type.Should().Be("EndSubsegment");
  }

  [Fact]
  public void AddAnnotation_ShouldRecordAnnotation()
  {
    // Arrange
    var service = new InMemoryTracingService();

    // Act
    service.AddAnnotation("key", "value");

    // Assert
    service.GetTraces().Should().HaveCount(1);
    var record = service.GetTraces().First();
    record.Type.Should().Be("Annotation");
    record.Key.Should().Be("key");
    record.Value.Should().Be("value");
  }

  [Fact]
  public void AddAnnotation_WithVariousTypes_ShouldRecordCorrectly()
  {
    // Arrange
    var service = new InMemoryTracingService();

    // Act
    service.AddAnnotation("string", "test");
    service.AddAnnotation("number", 42);
    service.AddAnnotation("boolean", true);
    service.AddAnnotation("null", null!);

    // Assert
    service.GetTraces().Should().HaveCount(4);
  }

  [Fact]
  public void AddMetadata_ShouldRecordMetadata()
  {
    // Arrange
    var service = new InMemoryTracingService();

    // Act
    service.AddMetadata("namespace", "key", "value");

    // Assert
    service.GetTraces().Should().HaveCount(1);
    var record = service.GetTraces().First();
    record.Type.Should().Be("Metadata");
    record.Namespace.Should().Be("namespace");
    record.Key.Should().Be("key");
    record.Value.Should().Be("value");
  }

  [Fact]
  public void RecordException_ShouldRecordException()
  {
    // Arrange
    var service = new InMemoryTracingService();
    var exception = new InvalidOperationException("Test error");

    // Act
    service.RecordException(exception);

    // Assert
    service.GetTraces().Should().HaveCount(1);
    var record = service.GetTraces().First();
    record.Type.Should().Be("Exception");
    record.Exception.Should().Be(exception);
  }

  [Fact]
  public void GetTraces_ShouldReturnReadOnlyList()
  {
    // Arrange
    var service = new InMemoryTracingService();
    service.AddAnnotation("test", "value");

    // Act
    var traces = service.GetTraces();

    // Assert
    traces.Should().BeAssignableTo<IReadOnlyList<TraceRecord>>();
  }

  [Fact]
  public void Clear_ShouldRemoveAllTraces()
  {
    // Arrange
    var service = new InMemoryTracingService();
    service.AddAnnotation("test1", "value1");
    service.AddAnnotation("test2", "value2");
    service.AddMetadata("ns", "key", "value");

    // Act
    service.Clear();

    // Assert
    service.GetTraces().Should().BeEmpty();
  }

  [Fact]
  public void MultipleTraces_ShouldTrackInOrder()
  {
    // Arrange
    var service = new InMemoryTracingService();

    // Act
    service.AddAnnotation("annotation1", "value1");
    using (var disposable = service.BeginSubsegment("segment1"))
    {
      service.AddMetadata("namespace1", "key1", "value1");
    }
    service.AddAnnotation("annotation2", "value2");
    service.RecordException(new Exception("test"));

    // Assert
    var traces = service.GetTraces().ToList();
    traces[0].Type.Should().Be("Annotation");
    traces[1].Type.Should().Be("BeginSubsegment");
    traces[2].Type.Should().Be("Metadata");
    traces[3].Type.Should().Be("EndSubsegment");
    traces[4].Type.Should().Be("Annotation");
    traces[5].Type.Should().Be("Exception");
  }

  [Fact]
  public void GetTraces_ShouldIncludeTraceId()
  {
    // Arrange
    var service = new InMemoryTracingService();
    var traceId = service.GetCurrentTraceId();
    service.AddAnnotation("test", "value");

    // Act
    var record = service.GetTraces().First();

    // Assert
    record.TraceId.Should().Be(traceId);
  }
}

