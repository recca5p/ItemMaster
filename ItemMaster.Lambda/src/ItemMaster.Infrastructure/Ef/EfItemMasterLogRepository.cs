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
                TraceId = traceId
            };

            _db.ItemLogs.Add(logRecord);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Successfully logged processing result: Operation={Operation}, Success={Success}, RequestSource={RequestSource}, ItemCount={ItemCount}, TraceId={TraceId}",
                operation, success, requestSource, itemCount, traceId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log processing result to MySQL: Operation={Operation}, Success={Success}, RequestSource={RequestSource}, TraceId={TraceId}",
                operation, success, requestSource, traceId);
            return Result.Failure($"MySQL logging failed: {ex.Message}");
        }
    }
}