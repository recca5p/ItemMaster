using ItemMaster.Domain;
using ItemMaster.Shared;

namespace ItemMaster.Integration.Tests;

public class SnowflakeDataHelper
{
    public static List<Item> CreateMockSnowflakeItems()
    {
        return new List<Item>
        {
            new()
            {
                Sku = "TEST-SKU-001",
                ProductTitle = "Test Product 1",
                Barcode = "1234567890123",
                SecondaryBarcode = "9876543210987",
                Hts = "1234567890",
                ChinaHts = "6543210987",
                CountryOfOrigin = "US",
                Price = 29.99f,
                Cost = 15.00f,
                LandedCost = 18.00f,
                Size = "M",
                Color = "Blue",
                Brand = "TestBrand",
                ProductType = "Apparel",
                Category = "Clothing",
                Gender = "Unisex",
                FabricContent = "Cotton",
                FabricComposition = "100% Cotton",
                VelocityCode = "V001",
                FastMover = "Y",
                InventorySyncFlag = "ON",
                CreatedAtSnowflake = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
                ProductImageUrl = "https://example.com/image1.jpg",
                ProductImageUrlPos1 = "https://example.com/pos1.jpg",
                ProductImageUrlPos2 = "https://example.com/pos2.jpg",
                ProductImageUrlPos3 = "https://example.com/pos3.jpg"
            },
            new()
            {
                Sku = "TEST-SKU-002",
                ProductTitle = "Test Product 2",
                Barcode = "1112223334445",
                SecondaryBarcode = "5556667778889",
                Hts = "9876543210",
                ChinaHts = "1234567890",
                CountryOfOrigin = "CN",
                Price = 49.99f,
                Cost = 25.00f,
                LandedCost = 30.00f,
                Size = "L",
                Color = "Red",
                Brand = "AnotherBrand",
                ProductType = "Accessories",
                Category = "Jewelry",
                Gender = "Female",
                FabricContent = "Leather",
                FabricComposition = "100% Leather",
                VelocityCode = "V002",
                FastMover = "N",
                InventorySyncFlag = "ON",
                CreatedAtSnowflake = new DateTimeOffset(2024, 2, 1, 8, 0, 0, TimeSpan.Zero),
                ProductImageUrl = "https://example.com/image2.jpg"
            }
        };
    }

    public static Result<IEnumerable<Item>> CreateMockSnowflakeResult(bool success = true)
    {
        if (!success)
            return Result<IEnumerable<Item>>.Failure("Mock Snowflake failure");

        return Result<IEnumerable<Item>>.Success(CreateMockSnowflakeItems());
    }
}