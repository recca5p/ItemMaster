namespace ItemMaster.Contracts;

public class ProcessSkusResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ItemsProcessed { get; set; }
    public int ItemsPublished { get; set; }
    public int Failed { get; set; }
    public List<string> SkusNotFound { get; set; } = new();
    public List<SkippedItemDetail> SkippedItems { get; set; } = new();
    public List<string> SuccessfulSkus { get; set; } = new();
    public List<PublishedItemDetail> PublishedItems { get; set; } = new();
}

public class SkippedItemDetail
{
    public string Sku { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ValidationFailure { get; set; } = string.Empty;
    public List<string> AllValidationErrors { get; set; } = new();
}

public class PublishedItemDetail
{
    public string Sku { get; set; } = string.Empty;
    public UnifiedItemMaster MappedItem { get; set; } = new();
    public List<string> SkippedProperties { get; set; } = new();
    public bool HasWarnings => SkippedProperties.Any();
}