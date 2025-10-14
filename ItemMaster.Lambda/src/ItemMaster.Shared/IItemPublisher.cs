using ItemMaster.Contracts;

namespace ItemMaster.Shared;

public interface IItemPublisher
{
    Task<Result> PublishUnifiedItemsAsync(IEnumerable<UnifiedItemMaster> items, string? traceId = null,
        CancellationToken cancellationToken = default);
}