using ItemMaster.Contracts;

namespace ItemMaster.Application.Services;

public class ItemMappingResult
{
    public List<UnifiedItemMaster> UnifiedItems { get; set; } = new();
    public List<SkippedItemDetail> SkippedItems { get; set; } = new();
    public List<string> SuccessfulSkus { get; set; } = new();
    public List<PublishedItemDetail> PublishedItems { get; set; } = new();
}