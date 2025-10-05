using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;

namespace ItemMaster.Infrastructure.Ef;

public sealed class EfItemMasterLogRepository : IItemMasterLogRepository
{
    private readonly ItemMasterDbContext _db;
    public EfItemMasterLogRepository(ItemMasterDbContext db) => _db = db;

    public async Task<int> InsertLogsAsync(IEnumerable<ItemLogRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        if (list.Count == 0) return 0;
        var entries = list.Select(r => new ItemLogEntry
        {
            Sku = r.Sku,
            Source = r.Source,
            RequestId = r.RequestId,
            TimestampUtc = r.TimestampUtc
        }).ToList();
        await _db.ItemLogs.AddRangeAsync(entries, ct);
        return await _db.SaveChangesAsync(ct);
    }
}

