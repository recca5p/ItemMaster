using Amazon.XRay.Recorder.Core;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure.Observability;

public interface ITracingService
{
    string? GetCurrentTraceId();
    IDisposable BeginSubsegment(string name);
    void AddAnnotation(string key, object value);
    void AddMetadata(string nameSpace, string key, object value);
    void RecordException(Exception exception);
}

public class XRayTracingService : ITracingService
{
    private readonly ILogger<XRayTracingService> _logger;

    public XRayTracingService(ILogger<XRayTracingService> logger)
    {
        _logger = logger;
    }

    public string? GetCurrentTraceId()
    {
        try
        {
            var traceContext = AWSXRayRecorder.Instance.GetEntity();
            return traceContext?.TraceId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current trace ID");
            return null;
        }
    }

    public IDisposable BeginSubsegment(string name)
    {
        try
        {
            AWSXRayRecorder.Instance.BeginSubsegment(name);
            return new SubsegmentDisposable();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to begin subsegment: {Name}", name);
            return new NoOpDisposable();
        }
    }

    public void AddAnnotation(string key, object value)
    {
        try
        {
            AWSXRayRecorder.Instance.AddAnnotation(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add annotation: {Key}={Value}", key, value);
        }
    }

    public void AddMetadata(string nameSpace, string key, object value)
    {
        try
        {
            AWSXRayRecorder.Instance.AddMetadata(nameSpace, key, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add metadata: {Namespace}.{Key}={Value}", nameSpace, key, value);
        }
    }

    public void RecordException(Exception exception)
    {
        try
        {
            AWSXRayRecorder.Instance.AddException(exception);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record exception in X-Ray");
        }
    }

    private class SubsegmentDisposable : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    AWSXRayRecorder.Instance.EndSubsegment();
                }
                catch (Exception)
                {
                }

                _disposed = true;
            }
        }
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

public class InMemoryTracingService : ITracingService
{
    private readonly string? _currentTraceId = Guid.NewGuid().ToString("N")[..16];
    private readonly List<TraceRecord> _traces = new();

    public string? GetCurrentTraceId()
    {
        return _currentTraceId;
    }

    public IDisposable BeginSubsegment(string name)
    {
        _traces.Add(new TraceRecord
        {
            Type = "BeginSubsegment",
            Name = name,
            Timestamp = DateTime.UtcNow,
            TraceId = _currentTraceId
        });

        return new InMemorySubsegment(name, this);
    }

    public void AddAnnotation(string key, object value)
    {
        _traces.Add(new TraceRecord
        {
            Type = "Annotation",
            Key = key,
            Value = value,
            Timestamp = DateTime.UtcNow,
            TraceId = _currentTraceId
        });
    }

    public void AddMetadata(string nameSpace, string key, object value)
    {
        _traces.Add(new TraceRecord
        {
            Type = "Metadata",
            Namespace = nameSpace,
            Key = key,
            Value = value,
            Timestamp = DateTime.UtcNow,
            TraceId = _currentTraceId
        });
    }

    public void RecordException(Exception exception)
    {
        _traces.Add(new TraceRecord
        {
            Type = "Exception",
            Exception = exception,
            Timestamp = DateTime.UtcNow,
            TraceId = _currentTraceId
        });
    }

    internal void EndSubsegment(string name)
    {
        _traces.Add(new TraceRecord
        {
            Type = "EndSubsegment",
            Name = name,
            Timestamp = DateTime.UtcNow,
            TraceId = _currentTraceId
        });
    }

    public IReadOnlyList<TraceRecord> GetTraces()
    {
        return _traces.AsReadOnly();
    }

    public void Clear()
    {
        _traces.Clear();
    }

    private class InMemorySubsegment : IDisposable
    {
        private readonly string _name;
        private readonly InMemoryTracingService _service;

        public InMemorySubsegment(string name, InMemoryTracingService service)
        {
            _name = name;
            _service = service;
        }

        public void Dispose()
        {
            _service.EndSubsegment(_name);
        }
    }
}

public class TraceRecord
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Namespace { get; set; }
    public string? Key { get; set; }
    public object? Value { get; set; }
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; }
    public string? TraceId { get; set; }
}