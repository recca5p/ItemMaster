using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure.Ef;

public sealed class EfItemMasterLogRepository : IItemMasterLogRepository
{
    private readonly IClock _clock;
    private readonly MySqlDbContext _db;
    private readonly ILogger<EfItemMasterLogRepository> _logger;

    public EfItemMasterLogRepository(
        MySqlDbContext db,
        IClock clock,
        ILogger<EfItemMasterLogRepository> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result> LogProcessingResultAsync(string operation, bool success, RequestSource requestSource,
        string? errorMessage = null, int? itemCount = null, string? traceId = null,
        string? sku = null, string? status = null, List<string>? skippedProperties = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var logRecord = new ItemLogRecord
            {
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

            _db.ItemLogs.Add(logRecord);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Successfully logged processing result: Operation={Operation}, Success={Success}, RequestSource={RequestSource}, ItemCount={ItemCount}, TraceId={TraceId}, Sku={Sku}, Status={Status}",
                operation, success, requestSource, itemCount, traceId, sku, status);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log processing result to MySQL: Operation={Operation}, Success={Success}, RequestSource={RequestSource}, TraceId={TraceId}, Sku={Sku}",
                operation, success, requestSource, traceId, sku);
            return Result.Failure($"MySQL logging failed: {ex.Message}");
        }
    }
}