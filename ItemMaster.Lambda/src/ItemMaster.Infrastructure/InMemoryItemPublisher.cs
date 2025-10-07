using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure;

public sealed class InMemoryItemPublisher : IItemPublisher
{
    private readonly ILogger<InMemoryItemPublisher> _logger;

    public InMemoryItemPublisher(ILogger<InMemoryItemPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<int> PublishAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken cancellationToken)
    {
        var count = skus.Count();
        _logger.LogInformation("Published {Count} items to in-memory queue", count);
        return Task.FromResult(count);
    }
}

