using ItemMaster.Domain;

namespace ItemMaster.Shared;

public interface ISnowflakeRepository
{
    Task<Result<IEnumerable<Item>>> GetItemsAsync(CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<Item>>> GetItemsBySkusAsync(IEnumerable<string> skus,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<Item>>> GetLatestItemsAsync(int count = 100, CancellationToken cancellationToken = default);
}