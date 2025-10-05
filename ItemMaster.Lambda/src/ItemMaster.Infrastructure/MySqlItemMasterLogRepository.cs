using System.Data;
using Dapper;
using MySqlConnector;
using ItemMaster.Shared;

namespace ItemMaster.Infrastructure;

public sealed class MySqlItemMasterLogRepository : IItemMasterLogRepository
{
    private readonly string _connectionString;
    private readonly string _tableName;

    public MySqlItemMasterLogRepository(string connectionString, string tableName = "item_master_logs")
    {
        _connectionString = connectionString;
        _tableName = tableName;
    }

    public async Task<int> InsertLogsAsync(IEnumerable<ItemLogRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        if (list.Count == 0) return 0;

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            const string sql = "INSERT INTO {0} (sku, source, request_id, timestamp_utc) VALUES (@Sku, @Source, @RequestId, @TimestampUtc)";
            var formatted = string.Format(sql, _tableName);
            var affected = await conn.ExecuteAsync(new CommandDefinition(formatted, list, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return affected;
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { /* ignore */ }
            throw;
        }
    }
}

// In-memory fallback repository for local tests when MySQL not configured
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
