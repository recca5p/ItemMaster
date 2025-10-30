using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Lambda.Tests.Infrastructure;

public class InMemoryItemMasterLogRepository : IItemMasterLogRepository
{
    private readonly IClock _clock;
    private readonly ILogger<InMemoryItemMasterLogRepository> _logger;
    private readonly List<ItemMasterSourceLog> _sourceLogs = new();

    public InMemoryItemMasterLogRepository(
        IClock clock,
        ILogger<InMemoryItemMasterLogRepository> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public Task<Result> LogItemSourceAsync(ItemMasterSourceLog log, CancellationToken cancellationToken = default)
    {
        log.Id = _sourceLogs.Count + 1;
        log.CreatedAt = _clock.UtcNow;
        _sourceLogs.Add(log);

        _logger.LogInformation(
            "InMemory: Logged item source - Sku: {Sku}, ValidationStatus: {ValidationStatus}, IsSentToSqs: {IsSentToSqs}",
            log.Sku, log.ValidationStatus, log.IsSentToSqs);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> MarkSentToSqsAsync(IEnumerable<string> skus, CancellationToken cancellationToken = default)
    {
        var skuSet = new HashSet<string>(skus.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
        if (skuSet.Count == 0) return Task.FromResult(Result.Success());

        foreach (var sku in skuSet)
        {
            var latest = _sourceLogs
                .Where(l => l.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefault();
            if (latest != null) latest.IsSentToSqs = true;
        }

        return Task.FromResult(Result.Success());
    }

    public IReadOnlyList<ItemMasterSourceLog> GetSourceLogs()
    {
        return _sourceLogs.AsReadOnly();
    }

    public void Clear()
    {
        _sourceLogs.Clear();
    }
}