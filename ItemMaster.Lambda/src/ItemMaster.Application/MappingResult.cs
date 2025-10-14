using ItemMaster.Contracts;

namespace ItemMaster.Application;

public class MappingResult
{
    public bool IsSuccess { get; set; }
    public UnifiedItemMaster? UnifiedItem { get; set; }
    public string? FailureReason { get; set; }
    public string Sku { get; set; } = string.Empty;

    public static MappingResult Success(UnifiedItemMaster item, string sku)
    {
        return new MappingResult
        {
            IsSuccess = true,
            UnifiedItem = item,
            Sku = sku
        };
    }

    public static MappingResult Failure(string sku, string reason)
    {
        return new MappingResult
        {
            IsSuccess = false,
            FailureReason = reason,
            Sku = sku
        };
    }
}