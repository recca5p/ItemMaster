using ItemMaster.Infrastructure.Ef;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure;

public class MySqlItemMasterLogRepository : IItemMasterLogRepository
{
    private readonly IClock _clock;
    private readonly MySqlDbContext _context;
    private readonly ILogger<MySqlItemMasterLogRepository> _logger;

    public MySqlItemMasterLogRepository(
        MySqlDbContext context,
        IClock clock,
        ILogger<MySqlItemMasterLogRepository> logger)
    {
        _context = context;
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

            _context.ItemLogs.Add(logRecord);
            await _context.SaveChangesAsync(cancellationToken);

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