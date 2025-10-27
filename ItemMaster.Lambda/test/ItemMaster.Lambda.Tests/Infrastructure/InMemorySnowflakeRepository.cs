using ItemMaster.Domain;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Lambda.Tests.Infrastructure;

public class InMemorySnowflakeRepository : ISnowflakeRepository
{
    private readonly ILogger<InMemorySnowflakeRepository> _logger;
    private readonly List<Item> _items = new();

    public InMemorySnowflakeRepository(ILogger<InMemorySnowflakeRepository> logger)
    {
        _logger = logger;
    }

    public Task<Result<IEnumerable<Item>>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("InMemory: Retrieved all {Count} items", _items.Count);
        return Task.FromResult(Result<IEnumerable<Item>>.Success(_items.AsEnumerable()));
    }

    public Task<Result<IEnumerable<Item>>> GetItemsBySkusAsync(IEnumerable<string> skus, CancellationToken cancellationToken = default)
    {
        var skuList = skus.ToList();
        var foundItems = _items.Where(i => skuList.Contains(i.Sku, StringComparer.OrdinalIgnoreCase)).ToList();
        
        _logger.LogInformation("InMemory: Retrieved {Count} items for {SkuCount} SKUs", foundItems.Count, skuList.Count);
        
        return Task.FromResult(Result<IEnumerable<Item>>.Success(foundItems));
    }

    public Task<Result<IEnumerable<Item>>> GetLatestItemsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var latestItems = _items.OrderByDescending(i => i.CreatedAtSnowflake).Take(limit).ToList();
        
        _logger.LogInformation("InMemory: Retrieved {Count} latest items (limit: {Limit})", latestItems.Count, limit);
        
        return Task.FromResult(Result<IEnumerable<Item>>.Success(latestItems));
    }

    public void AddItems(IEnumerable<Item> items)
    {
        _items.AddRange(items);
    }

    public void Clear()
    {
        _items.Clear();
    }

    public IReadOnlyList<Item> GetAllItems()
    {
        return _items.AsReadOnly();
    }
}
