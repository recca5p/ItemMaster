namespace ItemMaster.Shared;

public interface IItemPublisher
{
    Task<int> PublishAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken ct = default);
}

