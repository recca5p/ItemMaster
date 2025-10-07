namespace ItemMaster.Shared;

public interface IItemMasterLogRepository
{
    Task<int> InsertLogsAsync(IEnumerable<ItemLogRecord> records, CancellationToken ct = default);
}

