using ItemMaster.Contracts;
using ItemMaster.Domain;

namespace ItemMaster.Shared;

public interface IItemPublisher
{
    Task<Result> PublishItemsAsync(IEnumerable<Item> items, string? traceId = null,
        CancellationToken cancellationToken = default);

    Task<Result> PublishSimplifiedItemsAsync(IEnumerable<ItemForSqs> items, string? traceId = null,
        CancellationToken cancellationToken = default);
}