namespace ItemMaster.Infrastructure.Ef;

public sealed class ItemLogEntry
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
}