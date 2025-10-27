namespace ItemMaster.Infrastructure.Observability;

public interface ITracingService
{
    string? GetCurrentTraceId();
    IDisposable BeginSubsegment(string name);
    void AddAnnotation(string key, object value);
    void AddMetadata(string nameSpace, string key, object value);
    void RecordException(Exception exception);
}