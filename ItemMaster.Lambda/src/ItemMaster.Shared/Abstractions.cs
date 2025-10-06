namespace ItemMaster.Shared;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public interface IConfigProvider
{
    string? Get(string key, string? defaultValue = null);
    T Get<T>(string key, T defaultValue = default!);
}

public interface IItemMasterLogRepository
{
    Task<int> InsertLogsAsync(IEnumerable<ItemLogRecord> records, CancellationToken ct = default);
}

public record ItemLogRecord(string Sku, string Source, string RequestId, DateTime TimestampUtc);

public sealed class EnvConfigProvider : IConfigProvider
{
    public string? Get(string key, string? defaultValue = null)
        => Environment.GetEnvironmentVariable(key) ?? defaultValue;

    public T Get<T>(string key, T defaultValue = default!)
    {
        var raw = Get(key);
        if (raw is null) return defaultValue;
        try
        {
            return (T)Convert.ChangeType(raw, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}

public interface IConnectionStringProvider
{
    string? GetMySqlConnectionString();
}

public sealed class Result
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public static Result Ok() => new() { Success = true };
    public static Result Fail(string error) => new() { Success = false, Error = error };
}

public sealed class Result<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Value { get; init; }
    public static Result<T> Ok(T value) => new() { Success = true, Value = value };
    public static Result<T> Fail(string error) => new() { Success = false, Error = error };
}

public interface IItemPublisher
{
    Task<int> PublishAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken ct = default);
}
