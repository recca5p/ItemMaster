using ItemMaster.Domain;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure;

public class InMemorySnowflakeRepository : ISnowflakeRepository
{
    private readonly ILogger<InMemorySnowflakeRepository> _logger;

    public InMemorySnowflakeRepository(ILogger<InMemorySnowflakeRepository> logger)
    {
        _logger = logger;
    }

    public Task<Result<IEnumerable<Item>>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var items = GenerateTestItems(5);
        return Task.FromResult(Result<IEnumerable<Item>>.Success(items));
    }

    public Task<Result<IEnumerable<Item>>> GetItemsBySkusAsync(IEnumerable<string> skus,
        CancellationToken cancellationToken = default)
    {
        var skuList = skus.ToList();

        var items = skuList.Select((sku, index) => new Item
        {
            Sku = sku,
            Brand = $"TestBrand{index % 3 + 1}",
            Status = "Active",
            Region = "US",
            Barcode = $"123456789{index:D3}",
            ProductTitle = $"Test Product for {sku}",
            Price = 10.99f + index,
            CreatedAtSnowflake = DateTimeOffset.UtcNow.AddDays(-index)
        }).ToList();

        return Task.FromResult(Result<IEnumerable<Item>>.Success(items));
    }

    public Task<Result<IEnumerable<Item>>> GetLatestItemsAsync(int count = 100,
        CancellationToken cancellationToken = default)
    {
        var items = GenerateTestItems(Math.Min(count, 20))
            .OrderByDescending(i => i.CreatedAtSnowflake)
            .ToList();

        return Task.FromResult(Result<IEnumerable<Item>>.Success(items));
    }

    private List<Item> GenerateTestItems(int count)
    {
        return Enumerable.Range(1, count).Select(i => new Item
        {
            Sku = $"TEST-SKU-{i:D3}",
            Brand = $"TestBrand{i % 3 + 1}",
            Status = i % 4 == 0 ? "Inactive" : "Active",
            Region = i % 2 == 0 ? "US" : "EU",
            Barcode = $"123456789{i:D3}",
            ProductTitle = $"Test Product {i}",
            Price = 9.99f + i,
            CreatedAtSnowflake = DateTimeOffset.UtcNow.AddDays(-i),
            UpdatedAtSnowflake = DateTimeOffset.UtcNow.AddHours(-i)
        }).ToList();
    }
}