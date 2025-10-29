using System.Diagnostics.CodeAnalysis;
using ItemMaster.Domain;
using ItemMaster.Shared;

namespace ItemMaster.Infrastructure;

[ExcludeFromCodeCoverage]
public class MockSnowflakeRepository : ISnowflakeRepository
{
  private readonly List<Item> _mockItems;

  public MockSnowflakeRepository()
  {
    _mockItems = CreateMockItems();
  }

  public Task<Result<IEnumerable<Item>>> GetItemsAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(Result<IEnumerable<Item>>.Success(_mockItems));
  }

  public Task<Result<IEnumerable<Item>>> GetItemsBySkusAsync(IEnumerable<string> skus, CancellationToken cancellationToken = default)
  {
    var skuList = skus.Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (skuList.Count == 0)
      return Task.FromResult(Result<IEnumerable<Item>>.Success(Enumerable.Empty<Item>()));

    var results = _mockItems
        .Where(item => skuList.Any(sku => string.Equals(item.Sku, sku, StringComparison.OrdinalIgnoreCase)))
        .ToList();

    return Task.FromResult(Result<IEnumerable<Item>>.Success(results));
  }

  public Task<Result<IEnumerable<Item>>> GetLatestItemsAsync(int count = 100, CancellationToken cancellationToken = default)
  {
    var results = _mockItems
        .OrderByDescending(item => item.CreatedAtSnowflake)
        .Take(count)
        .ToList();

    return Task.FromResult(Result<IEnumerable<Item>>.Success(results));
  }

  private static List<Item> CreateMockItems()
  {
    var items = new List<Item>();
    items.AddRange(CreateBaseItems());
    items.AddRange(CreateTestItems());
    items.AddRange(CreateSqsTestItems());
    items.AddRange(CreateValidationTestItems());
    return items;
  }

  private static List<Item> CreateBaseItems()
  {
    return new List<Item>
        {
            new Item { Sku = "TEST-SKU-001", ProductTitle = "Test Product 1", Barcode = "1234567890123", SecondaryBarcode = "9876543210987", Hts = "1234567890", ChinaHts = "6543210987", CountryOfOrigin = "US", Price = 29.99f, Cost = 15.00f, LandedCost = 18.00f, Size = "M", Color = "Blue", Brand = "TestBrand", ProductType = "Apparel", Category = "Clothing", Gender = "Unisex", FabricContent = "Cotton", FabricComposition = "100% Cotton", VelocityCode = "V001", FastMover = "Y", InventorySyncFlag = "ON", CreatedAtSnowflake = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero) },
            new Item { Sku = "TEST-SKU-002", ProductTitle = "Test Product 2", Barcode = "1112223334445", SecondaryBarcode = "5556667778889", Hts = "9876543210", ChinaHts = "1234567890", CountryOfOrigin = "CN", Price = 49.99f, Cost = 25.00f, LandedCost = 30.00f, Size = "L", Color = "Red", Brand = "AnotherBrand", ProductType = "Accessories", Category = "Jewelry", Gender = "Female", FabricContent = "Leather", FabricComposition = "100% Leather", VelocityCode = "V002", FastMover = "N", InventorySyncFlag = "ON", CreatedAtSnowflake = new DateTimeOffset(2024, 2, 1, 8, 0, 0, TimeSpan.Zero) }
        };
  }

  private static List<Item> CreateTestItems()
  {
    return new List<Item>
        {
            new Item { Sku = "TEST-LOG-001", ProductTitle = "Test Log Product", Barcode = "9998887776665", Hts = "1234567890", CountryOfOrigin = "SG", Price = 19.99f, Cost = 10.00f, LandedCost = 12.00f, Size = "S", Color = "Green", FabricContent = "Polyester", FabricComposition = "100% Polyester", CreatedAtSnowflake = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "TEST-VALID-001", ProductTitle = "Valid Test Product", Barcode = "8887776665554", Hts = "0987654321", CountryOfOrigin = "US", Price = 39.99f, Cost = 20.00f, LandedCost = 25.00f, Size = "M", Color = "Black", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 7, 1, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "TEST-SOURCE-MODEL-001", ProductTitle = "Source Model Test", Barcode = "7776665554443", Hts = "1122334455", CountryOfOrigin = "UK", Price = 49.99f, Cost = 25.00f, LandedCost = 30.00f, Size = "L", Color = "White", FabricContent = "Silk", FabricComposition = "100% Silk", CreatedAtSnowflake = new DateTimeOffset(2024, 8, 1, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "TEST-COMMON-MODEL-001", ProductTitle = "Common Model Test", Barcode = "6665554443332", Hts = "2233445566", CountryOfOrigin = "JP", Price = 59.99f, Cost = 30.00f, LandedCost = 35.00f, Size = "XL", Color = "Blue", FabricContent = "Wool", FabricComposition = "100% Wool", CreatedAtSnowflake = new DateTimeOffset(2024, 9, 1, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "TEST-INVALID-001", ProductTitle = "Invalid Test Product", Barcode = "5554443332221", Hts = "3344556677", CountryOfOrigin = "CA", Price = 29.99f, Cost = 15.00f, LandedCost = 0f, Size = "S", Color = "Yellow" },
            new Item { Sku = "TEST-001", ProductTitle = "Test Item 1", Barcode = "1111111111111", Hts = "1111111111", CountryOfOrigin = "SG", Price = 19.99f, Cost = 10.00f, LandedCost = 12.00f, Size = "M", Color = "Blue", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 5, 1, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "TEST-002", ProductTitle = "Test Item 2", Barcode = "2222222222222", Hts = "2222222222", CountryOfOrigin = "SG", Price = 29.99f, Cost = 15.00f, LandedCost = 18.00f, Size = "L", Color = "Red", FabricContent = "Polyester", FabricComposition = "100% Polyester", CreatedAtSnowflake = new DateTimeOffset(2024, 5, 2, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "SKU-001", ProductTitle = "SKU Test 1", Barcode = "3030303030303", Hts = "3030303030", CountryOfOrigin = "SG", Price = 109.99f, Cost = 55.00f, LandedCost = 60.00f, Size = "XL", Color = "Brown", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 8, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "SKU-002", ProductTitle = "SKU Test 2", Barcode = "4040404040404", Hts = "4040404040", CountryOfOrigin = "SG", Price = 119.99f, Cost = 60.00f, LandedCost = 65.00f, Size = "M", Color = "Tan", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 9, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "SKU-000", ProductTitle = "SKU Test 0", Barcode = "5050505050505", Hts = "5050505050", CountryOfOrigin = "SG", Price = 129.99f, Cost = 65.00f, LandedCost = 70.00f, Size = "L", Color = "Beige", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 10, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "PRE-2024-SKU", ProductTitle = "Pre 2024 Test", Barcode = "6060606060606", SecondaryBarcode = "7070707070707", Hts = "6060606060", CountryOfOrigin = "SG", Price = 29.99f, Cost = 15.00f, LandedCost = 18.00f, Size = "M", Color = "Navy", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2023, 12, 15, 12, 0, 0, TimeSpan.Zero) }
        };
  }

  private static List<Item> CreateSqsTestItems()
  {
    return new List<Item>
        {
            new Item { Sku = "SQS-TEST-001", ProductTitle = "SQS Test Product", Barcode = "8080808080808", Hts = "8080808080", CountryOfOrigin = "US", Price = 39.99f, Cost = 20.00f, LandedCost = 25.00f, Size = "M", Color = "Green", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 1, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "SQS-CONTENT-001", ProductTitle = "SQS Content Test", Barcode = "9090909090909", Hts = "9090909090", CountryOfOrigin = "SG", Price = 49.99f, Cost = 25.00f, LandedCost = 30.00f, Size = "L", Color = "Purple", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 2, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "BATCH-001", ProductTitle = "Batch Test 1", Barcode = "1010101010101", Hts = "1010101010", CountryOfOrigin = "SG", Price = 59.99f, Cost = 30.00f, LandedCost = 35.00f, Size = "XL", Color = "Black", FabricContent = "Wool", FabricComposition = "100% Wool", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 3, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "BATCH-002", ProductTitle = "Batch Test 2", Barcode = "2020202020202", Hts = "2020202020", CountryOfOrigin = "SG", Price = 69.99f, Cost = 35.00f, LandedCost = 40.00f, Size = "M", Color = "White", FabricContent = "Silk", FabricComposition = "100% Silk", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 4, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "BATCH-003", ProductTitle = "Batch Test 3", Barcode = "3030303030303", Hts = "3030303030", CountryOfOrigin = "SG", Price = 79.99f, Cost = 40.00f, LandedCost = 45.00f, Size = "S", Color = "Gray", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 5, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "UNIQUE-001", ProductTitle = "Unique Test 1", Barcode = "4040404040404", Hts = "4040404040", CountryOfOrigin = "SG", Price = 89.99f, Cost = 45.00f, LandedCost = 50.00f, Size = "M", Color = "Orange", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 6, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "UNIQUE-002", ProductTitle = "Unique Test 2", Barcode = "5050505050505", Hts = "5050505050", CountryOfOrigin = "SG", Price = 99.99f, Cost = 50.00f, LandedCost = 55.00f, Size = "L", Color = "Pink", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 7, 12, 0, 0, TimeSpan.Zero) }
        };
  }

  private static List<Item> CreateValidationTestItems()
  {
    return new List<Item>
        {
            new Item { Sku = "INVALID-HTS", ProductTitle = "Invalid HTS Test", Barcode = "6060606060606", Hts = "INVALID", CountryOfOrigin = "SG", Price = 49.99f, Cost = 25.00f, LandedCost = 30.00f, Size = "M", Color = "Maroon", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 11, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "NO-LANDED-COST", ProductTitle = "No Landed Cost Test", Barcode = "7070707070707", Hts = "7070707070", CountryOfOrigin = "SG", Price = 59.99f, Cost = 30.00f, LandedCost = 0f, Size = "M", Color = "Cyan", FabricContent = "Cotton", FabricComposition = "100% Cotton", CreatedAtSnowflake = new DateTimeOffset(2024, 10, 12, 12, 0, 0, TimeSpan.Zero) },
            new Item { Sku = "NO-FABRIC", ProductTitle = "No Fabric Test", Barcode = "8080808080808", Hts = "8080808080", CountryOfOrigin = "SG", Price = 69.99f, Cost = 35.00f, LandedCost = 40.00f, Size = "M", Color = "Magenta" },
            new Item { Sku = "INVALID-SQS", ProductTitle = "Invalid SQS Test", Barcode = "9090909090909", Hts = "9090909090", CountryOfOrigin = "SG", Price = 79.99f, Cost = 40.00f, LandedCost = 0f, Size = "M", Color = "Lime" }
        };
  }
}
