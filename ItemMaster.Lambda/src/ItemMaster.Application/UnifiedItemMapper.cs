using ItemMaster.Contracts;
using ItemMaster.Domain;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application;

public class UnifiedItemMapper : IUnifiedItemMapper
{
    private readonly ILogger<UnifiedItemMapper> _logger;

    public UnifiedItemMapper(ILogger<UnifiedItemMapper> logger)
    {
        _logger = logger;
    }

    public MappingResult MapToUnifiedModel(Item item)
    {
        // Core validation: SKU, Name/Title, Barcode must be present
        if (string.IsNullOrWhiteSpace(item.Sku))
        {
            var reason = "Missing SKU";
            _logger.LogWarning("Item missing SKU - skipping");
            return MappingResult.Failure("UNKNOWN", reason);
        }

        if (string.IsNullOrWhiteSpace(item.ProductTitle))
        {
            var reason = "Missing ProductTitle (Name)";
            _logger.LogWarning("Item {Sku} missing ProductTitle - skipping", item.Sku);
            return MappingResult.Failure(item.Sku, reason);
        }

        // Either Barcode or SecondaryBarcode must have value
        if (string.IsNullOrWhiteSpace(item.Barcode) && string.IsNullOrWhiteSpace(item.SecondaryBarcode))
        {
            var reason = "Missing both Barcode and SecondaryBarcode";
            _logger.LogWarning("Item {Sku} missing both Barcode and SecondaryBarcode - skipping", item.Sku);
            return MappingResult.Failure(item.Sku, reason);
        }

        // HTS code validation - must be 10 characters
        if (string.IsNullOrWhiteSpace(item.Hts) || item.Hts.Length != 10)
        {
            var reason = $"Invalid HTS code (must be 10 digits, got: '{item.Hts}')";
            _logger.LogWarning("Item {Sku} has invalid HTS code - skipping", item.Sku);
            return MappingResult.Failure(item.Sku, reason);
        }

        // Country of Origin validation - must be 2 characters
        if (string.IsNullOrWhiteSpace(item.CountryOfOrigin) || item.CountryOfOrigin.Length != 2)
        {
            var reason = $"Invalid CountryOfOrigin (must be 2 chars, got: '{item.CountryOfOrigin}')";
            _logger.LogWarning("Item {Sku} has invalid CountryOfOrigin - skipping", item.Sku);
            return MappingResult.Failure(item.Sku, reason);
        }

        // Color and Size validation
        if (string.IsNullOrWhiteSpace(item.Color))
        {
            var reason = "Missing Color";
            _logger.LogWarning("Item {Sku} missing Color - skipping", item.Sku);
            return MappingResult.Failure(item.Sku, reason);
        }

        if (string.IsNullOrWhiteSpace(item.Size))
        {
            var reason = "Missing Size";
            _logger.LogWarning("Item {Sku} missing Size - skipping", item.Sku);
            return MappingResult.Failure(item.Sku, reason);
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

        // Determine which barcode to use based on CreatedAt date
        // For items created >= 2024-01-01, use Barcode as Gs1Barcode
        // For items created < 2024-01-01, use SecondaryBarcode as AlternateBarcodes
        if (item.CreatedAtSnowflake.HasValue &&
            item.CreatedAtSnowflake.Value >= new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))
        {
            unifiedItem.Gs1Barcode = item.Barcode;
            unifiedItem.AlternateBarcodes = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.SecondaryBarcode))
                unifiedItem.AlternateBarcodes.Add(item.SecondaryBarcode);
            if (!string.IsNullOrWhiteSpace(item.ThirdBarcode)) unifiedItem.AlternateBarcodes.Add(item.ThirdBarcode);
        }
        else
        {
            // For older items, use SecondaryBarcode as primary
            unifiedItem.Gs1Barcode =
                !string.IsNullOrWhiteSpace(item.SecondaryBarcode) ? item.SecondaryBarcode : item.Barcode;
            unifiedItem.AlternateBarcodes = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.Barcode) && item.Barcode != unifiedItem.Gs1Barcode)
                unifiedItem.AlternateBarcodes.Add(item.Barcode);
            if (!string.IsNullOrWhiteSpace(item.ThirdBarcode)) unifiedItem.AlternateBarcodes.Add(item.ThirdBarcode);
        }

        // Prices - list price is required
        unifiedItem.Prices = new List<PriceInfo>
        {
            new()
            {
                Type = "list",
                Currency = "USD",
                Value = (decimal)item.Price
            }
        };

        // Costs - unit and landed costs
        unifiedItem.Costs = new List<CostInfo>();
        if (item.Cost > 0 || item.LandedCost > 0)
        {
            unifiedItem.Costs.Add(new CostInfo
            {
                Type = "unit",
                Currency = "USD",
                Value = (decimal)item.Cost
            });
            unifiedItem.Costs.Add(new CostInfo
            {
                Type = "landed",
                Currency = "USD",
                Value = (decimal)item.LandedCost
            });
        }

        // Categories
        unifiedItem.Categories = new List<CategoryInfo>();
        if (!string.IsNullOrWhiteSpace(item.Category))
            unifiedItem.Categories.Add(new CategoryInfo
            {
                Path = item.Category,
                Source = "aka"
            });
        if (!string.IsNullOrWhiteSpace(item.ProductType))
            unifiedItem.Categories.Add(new CategoryInfo
            {
                Path = item.ProductType,
                Source = "brand"
            });

        // Attributes
        unifiedItem.Attributes = new List<AttributeInfo>
        {
            new() { Id = "size", Value = item.Size },
            new() { Id = "color", Value = item.Color }
        };

        if (!string.IsNullOrWhiteSpace(item.Brand))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "brand_name", Value = item.Brand });

        if (!string.IsNullOrWhiteSpace(item.FabricContent))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "fabric_content", Value = item.FabricContent });

        if (!string.IsNullOrWhiteSpace(item.FabricComposition))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "fabric_composition", Value = item.FabricComposition });

        if (!string.IsNullOrWhiteSpace(item.Gender))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "gender", Value = item.Gender });

        if (!string.IsNullOrWhiteSpace(item.VelocityCode))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "velocity_code", Value = item.VelocityCode });

        if (!string.IsNullOrWhiteSpace(item.FastMover))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "fast_mover", Value = item.FastMover });

        // Brand entity attribute
        if (!string.IsNullOrWhiteSpace(item.Brand))
            unifiedItem.Attributes.Add(new AttributeInfo { Id = "brand_entity", Value = item.Brand });

        // Inventory sync flag - default to "ON" if missing or invalid
        var inventorySyncValue = string.IsNullOrWhiteSpace(item.InventorySyncFlag) ? "ON" : item.InventorySyncFlag;
        unifiedItem.Attributes.Add(new AttributeInfo { Id = "inventory_sync_enabled", Value = inventorySyncValue });

        // Links
        unifiedItem.Links = new List<LinkInfo>();
        if (!string.IsNullOrWhiteSpace(item.ProductImageUrl))
            unifiedItem.Links.Add(new LinkInfo
            {
                Url = item.ProductImageUrl,
                Source = "Shopify US"
            });

        // Images
        unifiedItem.Images = new List<ImageInfo>();
        if (!string.IsNullOrWhiteSpace(item.ProductImageUrlPos1))
            unifiedItem.Images.Add(new ImageInfo
            {
                Url = item.ProductImageUrlPos1,
                SizeType = "original_size"
            });
        if (!string.IsNullOrWhiteSpace(item.ProductImageUrlPos2))
            unifiedItem.Images.Add(new ImageInfo
            {
                Url = item.ProductImageUrlPos2,
                SizeType = "original_size"
            });
        if (!string.IsNullOrWhiteSpace(item.ProductImageUrlPos3))
            unifiedItem.Images.Add(new ImageInfo
            {
                Url = item.ProductImageUrlPos3,
                SizeType = "original_size"
            });

        // Dates
        unifiedItem.Dates = new List<DateInfo>();
        if (item.CreatedAtShopify.HasValue)
            unifiedItem.Dates.Add(new DateInfo
            {
                CreatedAt = item.CreatedAtShopify.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                LastUpdatedAt = null,
                System = "shopify"
            });
        if (item.CreatedAtSnowflake.HasValue)
            unifiedItem.Dates.Add(new DateInfo
            {
                CreatedAt = item.CreatedAtSnowflake.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                LastUpdatedAt = item.UpdatedAtSnowflake?.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                System = "snowflake"
            });

        return MappingResult.Success(unifiedItem, item.Sku);
    }
}