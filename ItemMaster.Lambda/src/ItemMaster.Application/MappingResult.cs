using ItemMaster.Contracts;

namespace ItemMaster.Application;

public class MappingResult
{
    public bool IsSuccess { get; set; }
    public UnifiedItemMaster? UnifiedItem { get; set; }
    public string? FailureReason { get; set; }
    public string Sku { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> SkippedProperties { get; set; } = new();

    public static MappingResult Success(UnifiedItemMaster item, string sku, List<string>? skippedProperties = null)
    {
        return new MappingResult
        {
            IsSuccess = true,
            UnifiedItem = item,
            Sku = sku,
            SkippedProperties = skippedProperties ?? new List<string>()
        };
    }

    public static MappingResult Failure(string sku, List<string> validationErrors)
    {
        return new MappingResult
        {
            IsSuccess = false,
            ValidationErrors = validationErrors,
            Sku = sku,
            FailureReason = string.Join("; ", validationErrors)
        };
    }

    public static MappingResult Failure(string sku, string reason)
    {
        return new MappingResult
        {
            IsSuccess = false,
            FailureReason = reason,
            ValidationErrors = new List<string> { reason },
            Sku = sku
        };
    }
}