using ItemMaster.Shared;
using ItemMaster.Infrastructure.Ef;

namespace ItemMaster.Infrastructure;

public sealed class MySqlItemMasterLogRepository : IItemMasterLogRepository
{
    private readonly ItemMasterDbContext _db;
    public MySqlItemMasterLogRepository(ItemMasterDbContext db) => _db = db;

    public async Task<int> InsertLogsAsync(IEnumerable<ItemLogRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        if (list.Count == 0) return 0;
        var entities = list.Select(r => new ItemLogEntry
        {
            Sku = r.Sku,
            Source = r.Source,
            RequestId = r.RequestId,
            TimestampUtc = r.TimestampUtc
        }).ToList();
        await _db.ItemLogs.AddRangeAsync(entities, ct);
        return await _db.SaveChangesAsync(ct);
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
