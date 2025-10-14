using Microsoft.Extensions.Options;

namespace ItemMaster.Infrastructure;

public class SnowflakeItemQueryBuilder : ISnowflakeItemQueryBuilder
{
    private const string Projection = @"BRAND AS Brand,
        REGION AS Region,
        SKU AS Sku,
        STATUS AS Status,
        BARCODE AS Barcode,
        SECONDARY_BARCODE AS SecondaryBarcode,
        PRODUCT_TITLE AS ProductTitle,
        COLOR AS Color,
        SIZE AS Size,
        WEIGHT AS Weight,
        VOLUME AS Volume,
        HEIGHT AS Height,
        WIDTH AS Width,
        LENGTH AS Length,
        PRODUCT_TYPE AS ProductType,
        CATEGORY AS Category,
        GENDER AS Gender,
        FABRIC_CONTENT AS FabricContent,
        FABRIC_COMPOSITION AS FabricComposition,
        COUNTRY_OF_ORIGIN AS CountryOfOrigin,
        HTS AS Hts,
        CHINA_HTS AS ChinaHts,
        VELOCITY_CODE AS VelocityCode,
        FAST_MOVER AS FastMover,
        DESCRIPTION AS Description,
        PRODUCT_IMAGE_URL AS ProductImageUrl,
        PRODUCT_IMAGE_URL_POS_1 AS ProductImageUrlPos1,
        PRODUCT_IMAGE_URL_POS_2 AS ProductImageUrlPos2,
        PRODUCT_IMAGE_URL_POS_3 AS ProductImageUrlPos3,
        LANDED_COST AS LandedCost,
        COST AS Cost,
        PRICE AS Price,
        LATEST_PO_NUMBER AS LatestPoNumber,
        LATEST_PO_STATUS AS LatestPoStatus,
        LATEST_PO_CREATED_DATE AS LatestPoCreatedDate,
        LATEST_PO_EXPECTED_DATE AS LatestPoExpectedDate,
        WH_1_NAME AS Wh1Name,
        WH_1_AVAILABLE_QTY AS Wh1AvailableQty,
        WH_2_NAME AS Wh2Name,
        WH_2_AVAILABLE_QTY AS Wh2AvailableQty,
        WH_3_NAME AS Wh3Name,
        WH_3_AVAILABLE_QTY AS Wh3AvailableQty,
        CREATED_AT_SHOPIFY AS CreatedAtShopify,
        CREATED_AT_SNOWFLAKE AS CreatedAtSnowflake,
        UPDATED_AT_SNOWFLAKE AS UpdatedAtSnowflake,
        PRESENT_IN_XB_FLAG AS PresentInXbFlag,
        INVENTORY_SYNC_FLAG AS InventorySyncFlag,
        THIRD_BARCODE AS ThirdBarcode";

    private readonly string _fullyQualifiedTable;

    public SnowflakeItemQueryBuilder(IOptions<SnowflakeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var config = options.Value;

        if (string.IsNullOrWhiteSpace(config.Database))
            throw new ArgumentException("Snowflake database is required", nameof(options));
        if (string.IsNullOrWhiteSpace(config.Schema))
            throw new ArgumentException("Snowflake schema is required", nameof(options));
        if (string.IsNullOrWhiteSpace(config.Table))
            throw new ArgumentException("Snowflake table is required", nameof(options));

        _fullyQualifiedTable = $"{config.Database}.{config.Schema}.{config.Table}";
    }

    public string BuildSelectAll()
    {
        return $"SELECT {Projection} FROM {_fullyQualifiedTable} ORDER BY UPDATED_AT_SNOWFLAKE DESC";
    }

    public (string sql, object parameters) BuildSelectBySkus(IEnumerable<string> skus)
    {
        var sanitized = skus
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Where(s => IsSafeIdentifier(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sanitized.Count == 0)
        {
            var sqlEmpty =
                $"SELECT {Projection} FROM {_fullyQualifiedTable} WHERE 1=0 ORDER BY UPDATED_AT_SNOWFLAKE DESC";
            return (sqlEmpty, null);
        }

        var literals = string.Join(",", sanitized.Select(EscapeSqlLiteral));
        var sql =
            $"SELECT {Projection} FROM {_fullyQualifiedTable} WHERE SKU IN ({literals}) ORDER BY UPDATED_AT_SNOWFLAKE DESC";
        return (sql, null);
    }

    public string BuildSelectLatest(int count)
    {
        return
            $"SELECT {Projection} FROM {_fullyQualifiedTable} WHERE CREATED_AT_SNOWFLAKE IS NOT NULL ORDER BY CREATED_AT_SNOWFLAKE DESC LIMIT {count}";
    }

    private static bool IsSafeIdentifier(string s)
    {
        return s.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' || ch == '/');
    }

    private static string EscapeSqlLiteral(string s)
    {
        return "'" + s.Replace("'", "''") + "'";
    }
}