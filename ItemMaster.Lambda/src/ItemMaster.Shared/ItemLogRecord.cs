namespace ItemMaster.Shared;

public class ItemLogRecord
{
    public int Id { get; set; }
    public string Operation { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ItemCount { get; set; }
    public DateTime Timestamp { get; set; }
    public RequestSource RequestSource { get; set; } = RequestSource.Unknown;
    public string? TraceId { get; set; }
    public string? Sku { get; set; }
    public string? Status { get; set; }
    public string? SkippedProperties { get; set; }
}