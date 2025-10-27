namespace ItemMaster.Infrastructure.Observability;

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
