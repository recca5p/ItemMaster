using System.Diagnostics.CodeAnalysis;

namespace ItemMaster.Shared;

[ExcludeFromCodeCoverage]
public class ItemMasterSourceLog
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? SourceModel { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public string? CommonModel { get; set; }
    public string? Errors { get; set; }
    public bool IsSentToSqs { get; set; }
    public DateTime CreatedAt { get; set; }
}