using ItemMaster.Contracts;
using ItemMaster.Domain;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure;

public class InMemoryItemPublisher : IItemPublisher
{
    private readonly ILogger<InMemoryItemPublisher> _logger;
    private readonly List<Item> _publishedItems = new();

    public InMemoryItemPublisher(ILogger<InMemoryItemPublisher> logger)
    {
        _logger = logger;
    }

    public Task<Result> PublishItemsAsync(IEnumerable<Item> items, string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        _publishedItems.AddRange(itemList);

        _logger.LogInformation("Published {ItemCount} items to in-memory store (test mode) | TraceId: {TraceId}",
            itemList.Count, traceId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> PublishSimplifiedItemsAsync(IEnumerable<ItemForSqs> items, string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();

        _logger.LogInformation(
            "Published {ItemCount} simplified items to in-memory store (test mode) | TraceId: {TraceId}",
            itemList.Count, traceId);

        return Task.FromResult(Result.Success());
    }

    public IReadOnlyList<Item> GetPublishedItems()
    {
        return _publishedItems.AsReadOnly();
    }

    public void Clear()
    {
        _publishedItems.Clear();
    }
}