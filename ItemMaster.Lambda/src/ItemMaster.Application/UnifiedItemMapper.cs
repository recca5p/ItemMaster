using ItemMaster.Contracts;
using ItemMaster.Domain;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application;

public class UnifiedItemMapper : IUnifiedItemMapper
{
    // Constants for validation rules
    private const int RequiredHtsCodeLength = 10;
    private const int RequiredCountryCodeLength = 2;
    private const string DefaultCurrency = "USD";
    private const string PriceTypeList = "list";
    private const string CostTypeUnit = "unit";
    private const string CostTypeLanded = "landed";
    private const string CategorySourceAka = "aka";
    private const string CategorySourceBrand = "brand";
    private const string InventorySyncDefault = "ON";
    private const string ImageSizeType = "original_size";
    private const string ShopifyLinkSource = "Shopify US";

    // Date constants
    private static readonly DateTimeOffset BarcodeLogicCutoffDate = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ILogger<UnifiedItemMapper> _logger;

    public UnifiedItemMapper(ILogger<UnifiedItemMapper> logger)
    {
        _logger = logger;
    }

    public MappingResult MapToUnifiedModel(Item item)
    {
        var validationErrors = new List<string>();
        var skippedProperties = new List<string>();

        if (string.IsNullOrWhiteSpace(item.Sku)) validationErrors.Add("Missing SKU");

        if (string.IsNullOrWhiteSpace(item.ProductTitle)) validationErrors.Add("Missing ProductTitle (Name)");

        if (string.IsNullOrWhiteSpace(item.Barcode) && string.IsNullOrWhiteSpace(item.SecondaryBarcode))
            validationErrors.Add("Missing both Barcode and SecondaryBarcode");

        if (string.IsNullOrWhiteSpace(item.Hts) || item.Hts.Length != RequiredHtsCodeLength)
            validationErrors.Add($"Invalid HTS code (must be {RequiredHtsCodeLength} digits, got: '{item.Hts}')");

        if (string.IsNullOrWhiteSpace(item.CountryOfOrigin) || item.CountryOfOrigin.Length != RequiredCountryCodeLength)
            validationErrors.Add(
                $"Invalid CountryOfOrigin (must be {RequiredCountryCodeLength} chars, got: '{item.CountryOfOrigin}')");

        if (string.IsNullOrWhiteSpace(item.Color)) validationErrors.Add("Missing Color");

        if (string.IsNullOrWhiteSpace(item.Size)) validationErrors.Add("Missing Size");
        if (validationErrors.Any())
        {
            var sku = string.IsNullOrWhiteSpace(item.Sku) ? "UNKNOWN" : item.Sku;
            _logger.LogWarning("Item {Sku} failed initial validation with {ErrorCount} errors: {Errors}",
                sku, validationErrors.Count, string.Join("; ", validationErrors));
            return MappingResult.Failure(sku, validationErrors);
        }

        var unifiedItem = new UnifiedItemMaster
        {
            Sku = item.Sku,
            Name = item.ProductTitle,
            Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description,
            HtsTariffCode = item.Hts,
            HsCommodityCode = item.Hts.Length >= 6 ? item.Hts.Substring(0, 6) : null,
            ChinaHtsCode = string.IsNullOrWhiteSpace(item.ChinaHts) ? null : item.ChinaHts,
            CountryOfOriginCode = item.CountryOfOrigin
        };

        if (string.IsNullOrWhiteSpace(item.Description))
            skippedProperties.Add("Description");

        if (string.IsNullOrWhiteSpace(item.ChinaHts))
            skippedProperties.Add("ChinaHtsCode");

        if (item.CreatedAtSnowflake.HasValue &&
            item.CreatedAtSnowflake.Value >= BarcodeLogicCutoffDate)
        {
            unifiedItem.Gs1Barcode = item.Barcode;
            unifiedItem.AlternateBarcodes = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.SecondaryBarcode))
                unifiedItem.AlternateBarcodes.Add(item.SecondaryBarcode);
            if (!string.IsNullOrWhiteSpace(item.ThirdBarcode)) unifiedItem.AlternateBarcodes.Add(item.ThirdBarcode);
        }
        else
        {
            unifiedItem.Gs1Barcode =
                !string.IsNullOrWhiteSpace(item.SecondaryBarcode) ? item.SecondaryBarcode : item.Barcode;
            unifiedItem.AlternateBarcodes = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.Barcode) && item.Barcode != unifiedItem.Gs1Barcode)
                unifiedItem.AlternateBarcodes.Add(item.Barcode);
            if (!string.IsNullOrWhiteSpace(item.ThirdBarcode)) unifiedItem.AlternateBarcodes.Add(item.ThirdBarcode);
        }

        unifiedItem.Prices = new List<PriceInfo>
        {
            new()
            {
                Type = "list",
                Currency = "USD",
                Value = (decimal)item.Price
            }
        };

        unifiedItem.Costs = new List<CostInfo>();

        if (item.Cost > 0)
            unifiedItem.Costs.Add(new CostInfo
            {
                Type = "unit",
                Currency = "USD",
                Value = (decimal)item.Cost
            });

        if (item.LandedCost > 0)
            unifiedItem.Costs.Add(new CostInfo
            {
                Type = "landed",
                Currency = "USD",
                Value = (decimal)item.LandedCost
            });
        else
            validationErrors.Add("Missing required LandedCost");

        unifiedItem.Categories = new List<CategoryInfo>();
        if (!string.IsNullOrWhiteSpace(item.Category))
            unifiedItem.Categories.Add(new CategoryInfo
            {
                Path = item.Category,
                Source = "aka"
            });
        else
            skippedProperties.Add("Category.aka");

        if (!string.IsNullOrWhiteSpace(item.ProductType))
            unifiedItem.Categories.Add(new CategoryInfo
            {
                Path = item.ProductType,
                Source = "brand"
            });
        else
            skippedProperties.Add("Category.brand");

        unifiedItem.Attributes = new List<AttributeInfo>
        {
            new() { Id = "size", Value = item.Size },
            new() { Id = "color", Value = item.Color }
        };

        if (!string.IsNullOrWhiteSpace(item.Brand))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "brand_name", Value = item.Brand });
        else
            skippedProperties.Add("Attribute.brand_name");

        if (!string.IsNullOrWhiteSpace(item.FabricContent))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "fabric_content", Value = item.FabricContent });
        else
            validationErrors.Add("Missing required FabricContent");

        if (!string.IsNullOrWhiteSpace(item.FabricComposition))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "fabric_composition", Value = item.FabricComposition });
        else
            validationErrors.Add("Missing required FabricComposition");

        if (!string.IsNullOrWhiteSpace(item.Gender))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "gender", Value = item.Gender });
        else
            skippedProperties.Add("Attribute.gender");

        if (!string.IsNullOrWhiteSpace(item.VelocityCode))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "velocity_code", Value = item.VelocityCode });
        else
            skippedProperties.Add("Attribute.velocity_code");

        if (!string.IsNullOrWhiteSpace(item.FastMover))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "fast_mover", Value = item.FastMover });
        else
            skippedProperties.Add("Attribute.fast_mover");

        if (!string.IsNullOrWhiteSpace(item.Brand))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "brand_entity", Value = item.Brand });

        var inventorySyncValue = string.IsNullOrWhiteSpace(item.InventorySyncFlag)
            ? InventorySyncDefault
            : item.InventorySyncFlag;
        unifiedItem.Attributes.Add(new AttributeInfo { Id = "inventory_sync_enabled", Value = inventorySyncValue });

        unifiedItem.Links = new List<LinkInfo>();
        if (!string.IsNullOrWhiteSpace(item.ProductImageUrl))
            unifiedItem.Links.Add(new LinkInfo
            {
                Url = item.ProductImageUrl,
                Source = "Shopify US"
            });
        else
            skippedProperties.Add("Link.ProductImageUrl");

        unifiedItem.Images = new List<ImageInfo>();
        if (!string.IsNullOrWhiteSpace(item.ProductImageUrlPos1))
            unifiedItem.Images.Add(new ImageInfo
            {
                Url = item.ProductImageUrlPos1,
                SizeType = "original_size"
            });
        else
            skippedProperties.Add("Image.Pos1");

        if (!string.IsNullOrWhiteSpace(item.ProductImageUrlPos2))
            unifiedItem.Images.Add(new ImageInfo
            {
                Url = item.ProductImageUrlPos2,
                SizeType = "original_size"
            });
        else
            skippedProperties.Add("Image.Pos2");

        if (!string.IsNullOrWhiteSpace(item.ProductImageUrlPos3))
            unifiedItem.Images.Add(new ImageInfo
            {
                Url = item.ProductImageUrlPos3,
                SizeType = "original_size"
            });
        else
            skippedProperties.Add("Image.Pos3");

        unifiedItem.Dates = new List<DateInfo>();
        if (item.CreatedAtShopify.HasValue)
            unifiedItem.Dates.Add(new DateInfo
            {
                CreatedAt = item.CreatedAtShopify.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                LastUpdatedAt = null,
                System = "shopify"
            });
        else
            skippedProperties.Add("Date.Shopify");

        if (item.CreatedAtSnowflake.HasValue)
            unifiedItem.Dates.Add(new DateInfo
            {
                CreatedAt = item.CreatedAtSnowflake.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                LastUpdatedAt = item.UpdatedAtSnowflake?.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                System = "snowflake"
            });
        else
            skippedProperties.Add("Date.Snowflake");

        if (validationErrors.Any())
        {
            var sku = string.IsNullOrWhiteSpace(item.Sku) ? "UNKNOWN" : item.Sku;
            _logger.LogWarning("Item {Sku} failed validation with {ErrorCount} errors: {Errors}",
                sku, validationErrors.Count, string.Join("; ", validationErrors));
            return MappingResult.Failure(sku, validationErrors);
        }

        if (skippedProperties.Any())
            _logger.LogInformation(
                "Item {Sku} mapped successfully with {SkippedCount} optional properties skipped: {Properties}",
                item.Sku, skippedProperties.Count, string.Join(", ", skippedProperties));

        return MappingResult.Success(unifiedItem, item.Sku, skippedProperties);
    }
}