using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure;

public class InMemoryItemMasterLogRepository : IItemMasterLogRepository
{
    private readonly IClock _clock;
    private readonly ILogger<InMemoryItemMasterLogRepository> _logger;
    private readonly List<ItemLogRecord> _logs = new();

    public InMemoryItemMasterLogRepository(
        IClock clock,
        ILogger<InMemoryItemMasterLogRepository> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public Task<Result> LogProcessingResultAsync(string operation, bool success, RequestSource requestSource,
        string? errorMessage = null, int? itemCount = null, string? traceId = null,
        string? sku = null, string? status = null, List<string>? skippedProperties = null,
        CancellationToken cancellationToken = default)
    {
        var logRecord = new ItemLogRecord
        {
            Id = _logs.Count + 1,
            Operation = operation,
            Success = success,
            ErrorMessage = errorMessage,
            ItemCount = itemCount,
            Timestamp = _clock.UtcNow,
            RequestSource = requestSource,
            TraceId = traceId,
            Sku = sku,
            Status = status,
            SkippedProperties = skippedProperties != null && skippedProperties.Any() 
                ? string.Join(", ", skippedProperties) 
                : null
        };

        _logs.Add(logRecord);

        _logger.LogInformation(
            "InMemory: Logged processing result - Operation: {Operation}, Success: {Success}, RequestSource: {RequestSource}, ItemCount: {ItemCount}, TraceId: {TraceId}, Sku: {Sku}, Status: {Status}",
            operation, success, requestSource, itemCount, traceId, sku, status);

        return Task.FromResult(Result.Success());
    }

    public IReadOnlyList<ItemLogRecord> GetLogs()
    {
        return _logs.AsReadOnly();
    }

    public void Clear()
    {
        _logs.Clear();
    }
}