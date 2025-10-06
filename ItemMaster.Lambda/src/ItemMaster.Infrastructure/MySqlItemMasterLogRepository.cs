using ItemMaster.Shared;
using ItemMaster.Infrastructure.Ef;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ItemMaster.Infrastructure;

public sealed class MySqlItemMasterLogRepository : IItemMasterLogRepository
{
    private readonly ItemMasterDbContext _db;
    private readonly ILogger<MySqlItemMasterLogRepository> _logger;
    public MySqlItemMasterLogRepository(ItemMasterDbContext db, ILogger<MySqlItemMasterLogRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> InsertLogsAsync(IEnumerable<ItemLogRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        if (list.Count == 0)
        {
            _logger.LogDebug("InsertSkipEmpty");
            return 0;
        }
        var sw = Stopwatch.StartNew();
        try
        {
            var entities = list.Select(r => new ItemLogEntry
            {
                Sku = r.Sku,
                Source = r.Source,
                RequestId = r.RequestId,
                TimestampUtc = r.TimestampUtc
            }).ToList();
            await _db.ItemLogs.AddRangeAsync(entities, ct);
            var written = await _db.SaveChangesAsync(ct);
            sw.Stop();
            _logger.LogInformation("InsertSuccess count={Count} elapsedMs={ElapsedMs}", list.Count, sw.ElapsedMilliseconds);
            return written;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "InsertFailure count={Count} elapsedMs={ElapsedMs}", list.Count, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

public sealed class InMemoryItemMasterLogRepository : IItemMasterLogRepository
{
    private readonly List<ItemLogRecord> _store = new();
    public Task<int> InsertLogsAsync(IEnumerable<ItemLogRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        _store.AddRange(list);
        return Task.FromResult(list.Count);
    }
    public IReadOnlyCollection<ItemLogRecord> Records => _store.AsReadOnly();
}
