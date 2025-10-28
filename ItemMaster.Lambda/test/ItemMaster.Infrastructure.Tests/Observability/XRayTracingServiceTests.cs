using Amazon.XRay.Recorder.Core;
using FluentAssertions;
using ItemMaster.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Observability;

public class XRayTracingServiceTests
{
  private readonly Mock<ILogger<XRayTracingService>> _mockLogger;
  private readonly XRayTracingService _service;

  public XRayTracingServiceTests()
  {
    _mockLogger = new Mock<ILogger<XRayTracingService>>();
    _service = new XRayTracingService(_mockLogger.Object);
  }

  [Fact]
  public void GetCurrentTraceId_ShouldHandleExceptionGracefully()
  {
    // Act
    var traceId = _service.GetCurrentTraceId();

    // Assert
    traceId.Should().BeNull();
  }

  [Fact]
  public void BeginSubsegment_ShouldReturnDisposable()
  {
    // Act
    var disposable = _service.BeginSubsegment("test-segment");

    // Assert
    disposable.Should().NotBeNull();
  }

  [Fact]
  public void BeginSubsegment_ShouldHandleExceptionsGracefully()
  {
    // Act
    var disposable = _service.BeginSubsegment("test-segment");

    disposable.Should().NotBeNull();

    var act = () => disposable.Dispose();
    
    // Assert
    act.Should().NotThrow();
  }

  [Fact]
  public void AddAnnotation_ShouldHandleExceptionsGracefully()
  {
    // Act
    var act = () => _service.AddAnnotation("key", "value");

    // Assert
    act.Should().NotThrow();
  }

  [Fact]
  public void AddMetadata_ShouldHandleExceptionsGracefully()
  {
    // Act
    var act = () => _service.AddMetadata("namespace", "key", "value");

    // Assert
    act.Should().NotThrow();
  }

  [Fact]
  public void RecordException_ShouldHandleExceptionsGracefully()
  {
    // Arrange
    var exception = new InvalidOperationException("Test error");

    // Act
    var act = () => _service.RecordException(exception);

    // Assert
    act.Should().NotThrow();
  }

  [Fact]
  public void BeginSubsegment_MultipleDispose_ShouldNotThrow()
  {
    // Act
    var disposable = _service.BeginSubsegment("test");

    disposable.Dispose();

    // Assert
    disposable.Should().NotBeNull();
  }

  [Fact]
  public void AddAnnotation_WithVariousValueTypes_ShouldHandleCorrectly()
  {
    // Act
    var act1 = () => _service.AddAnnotation("string", "test");
    var act2 = () => _service.AddAnnotation("number", 42);
    var act3 = () => _service.AddAnnotation("boolean", true);

    // Assert
    act1.Should().NotThrow();
    act2.Should().NotThrow();
    act3.Should().NotThrow();
  }

  [Fact]
  public void AddMetadata_WithVariousValueTypes_ShouldHandleCorrectly()
  {
    // Act
    var act1 = () => _service.AddMetadata("ns", "key1", "string");
    var act2 = () => _service.AddMetadata("ns", "key2", 42);
    var act3 = () => _service.AddMetadata("ns", "key3", true);
    var act4 = () => _service.AddMetadata("ns", "key4", new { Prop = "value" });

    // Assert
    act1.Should().NotThrow();
    act2.Should().NotThrow();
    act3.Should().NotThrow();
    act4.Should().NotThrow();
  }

  [Fact]
  public void RecordException_WithVariousExceptionTypes_ShouldHandleCorrectly()
  {
    // Act
    var act1 = () => _service.RecordException(new ArgumentException("test"));
    var act2 = () => _service.RecordException(new InvalidOperationException("test"));
    var act3 = () => _service.RecordException(new NullReferenceException("test"));

    // Assert
    act1.Should().NotThrow();
    act2.Should().NotThrow();
    act3.Should().NotThrow();
  }

  [Fact]
  public void BeginSubsegment_WithMultipleSegments_ShouldHandleCorrectly()
  {
    // Act
    var segment1 = _service.BeginSubsegment("segment1");
    var segment2 = _service.BeginSubsegment("segment2");

    // Assert
    segment1.Should().NotBeNull();
    segment2.Should().NotBeNull();
    segment1.Should().NotBeSameAs(segment2);

    segment1.Dispose();
    segment2.Dispose();
  }

  [Fact]
  public void GetCurrentTraceId_ShouldNotThrow()
  {
    // Act
    var traceId = _service.GetCurrentTraceId();

    // Assert
    traceId.Should().BeNull();
  }

  [Fact]
  public void AddAnnotation_WithNullValues_ShouldHandleGracefully()
  {
    // Act
    var act1 = () => _service.AddAnnotation("key", null!);
    var act2 = () => _service.AddAnnotation(null!, "value");

    // Assert
    act1.Should().NotThrow();
    act2.Should().NotThrow();
  }

  [Fact]
  public void AddMetadata_WithNullValues_ShouldHandleGracefully()
  {
    // Act
    var act1 = () => _service.AddMetadata("ns", "key", null!);
    var act2 = () => _service.AddMetadata("ns", null!, "value");
    var act3 = () => _service.AddMetadata(null!, "key", "value");

    // Assert
    act1.Should().NotThrow();
    act2.Should().NotThrow();
    act3.Should().NotThrow();
  }
}

