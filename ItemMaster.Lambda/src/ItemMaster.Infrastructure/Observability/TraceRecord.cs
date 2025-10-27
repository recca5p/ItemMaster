namespace ItemMaster.Infrastructure.Observability;

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