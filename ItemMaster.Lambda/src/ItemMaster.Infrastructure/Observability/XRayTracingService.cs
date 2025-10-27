using Amazon.XRay.Recorder.Core;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure.Observability;

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
