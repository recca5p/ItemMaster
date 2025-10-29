using System.Diagnostics.CodeAnalysis;

namespace ItemMaster.Domain;

[ExcludeFromCodeCoverage]
public class Item
{
    public string Brand { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string SecondaryBarcode { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public float Weight { get; set; }
    public float Volume { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }
    public float Length { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string FabricContent { get; set; } = string.Empty;
    public string FabricComposition { get; set; } = string.Empty;
    public string CountryOfOrigin { get; set; } = string.Empty;
    public string Hts { get; set; } = string.Empty;
    public string ChinaHts { get; set; } = string.Empty;
    public string VelocityCode { get; set; } = string.Empty;
    public string FastMover { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public string ProductImageUrlPos1 { get; set; } = string.Empty;
    public string ProductImageUrlPos2 { get; set; } = string.Empty;
    public string ProductImageUrlPos3 { get; set; } = string.Empty;
    public float LandedCost { get; set; }
    public float Cost { get; set; }
    public float Price { get; set; }
    public string LatestPoNumber { get; set; } = string.Empty;
    public string LatestPoStatus { get; set; } = string.Empty;
    public DateTimeOffset? LatestPoCreatedDate { get; set; }
    public DateTimeOffset? LatestPoExpectedDate { get; set; }
    public string Wh1Name { get; set; } = string.Empty;
    public decimal Wh1AvailableQty { get; set; }
    public string Wh2Name { get; set; } = string.Empty;
    public decimal Wh2AvailableQty { get; set; }
    public string Wh3Name { get; set; } = string.Empty;
    public decimal Wh3AvailableQty { get; set; }
    public DateTimeOffset? CreatedAtShopify { get; set; }
    public DateTimeOffset? CreatedAtSnowflake { get; set; }
    public DateTimeOffset? UpdatedAtSnowflake { get; set; }
    public string PresentInXbFlag { get; set; } = string.Empty;
    public string InventorySyncFlag { get; set; } = string.Empty;
    public string ThirdBarcode { get; set; } = string.Empty;
}