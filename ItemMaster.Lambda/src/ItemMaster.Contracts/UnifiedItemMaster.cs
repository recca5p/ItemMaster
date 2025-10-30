using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ItemMaster.Contracts;

[ExcludeFromCodeCoverage]
public class UnifiedItemMaster
{
    [JsonPropertyName("Sku")] public string Sku { get; set; } = string.Empty;

    [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Gs1Barcode")] public string? Gs1Barcode { get; set; }

    [JsonPropertyName("AlternateBarcodes")]
    public List<string> AlternateBarcodes { get; set; } = new();

    [JsonPropertyName("Description")] public string? Description { get; set; }

    [JsonPropertyName("HtsTariffCode")] public string? HtsTariffCode { get; set; }

    [JsonPropertyName("HsCommodityCode")] public string? HsCommodityCode { get; set; }

    [JsonPropertyName("ChinaHtsCode")] public string? ChinaHtsCode { get; set; }

    [JsonPropertyName("CountryOfOriginCode")]
    public string CountryOfOriginCode { get; set; } = string.Empty;

    [JsonPropertyName("Prices")] public List<PriceInfo> Prices { get; set; } = new();

    [JsonPropertyName("Costs")] public List<CostInfo> Costs { get; set; } = new();

    [JsonPropertyName("Categories")] public List<CategoryInfo> Categories { get; set; } = new();

    [JsonPropertyName("Attributes")] public List<AttributeInfo> Attributes { get; set; } = new();

    [JsonPropertyName("Links")] public List<LinkInfo> Links { get; set; } = new();

    [JsonPropertyName("Images")] public List<ImageInfo> Images { get; set; } = new();

    [JsonPropertyName("Dates")] public List<DateInfo> Dates { get; set; } = new();
}

[ExcludeFromCodeCoverage]
public class PriceInfo
{
    [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;

    [JsonPropertyName("Currency")] public string Currency { get; set; } = "USD";

    [JsonPropertyName("Value")] public decimal Value { get; set; }
}

[ExcludeFromCodeCoverage]
public class CostInfo
{
    [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;

    [JsonPropertyName("Currency")] public string Currency { get; set; } = "USD";

    [JsonPropertyName("Value")] public decimal Value { get; set; }
}

[ExcludeFromCodeCoverage]
public class CategoryInfo
{
    [JsonPropertyName("Path")] public string Path { get; set; } = string.Empty;

    [JsonPropertyName("Source")] public string Source { get; set; } = string.Empty;
}

[ExcludeFromCodeCoverage]
public class AttributeInfo
{
    [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Value")] public string Value { get; set; } = string.Empty;
}

[ExcludeFromCodeCoverage]
public class LinkInfo
{
    [JsonPropertyName("Url")] public string Url { get; set; } = string.Empty;

    [JsonPropertyName("Source")] public string Source { get; set; } = string.Empty;
}

[ExcludeFromCodeCoverage]
public class ImageInfo
{
    [JsonPropertyName("Url")] public string Url { get; set; } = string.Empty;

    [JsonPropertyName("SizeType")] public string SizeType { get; set; } = string.Empty;
}

[ExcludeFromCodeCoverage]
public class DateInfo
{
    [JsonPropertyName("CreatedAt")] public string? CreatedAt { get; set; }

    [JsonPropertyName("LastUpdatedAt")] public string? LastUpdatedAt { get; set; }

    [JsonPropertyName("System")] public string System { get; set; } = string.Empty;
}