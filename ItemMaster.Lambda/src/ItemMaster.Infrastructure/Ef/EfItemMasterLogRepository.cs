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


    public async Task<Result> LogItemSourceAsync(ItemMasterSourceLog log, CancellationToken cancellationToken = default)
    {
        try
        {
            log.CreatedAt = _clock.UtcNow;
            _db.ItemMasterSourceLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Successfully logged item source: Sku={Sku}, ValidationStatus={ValidationStatus}, IsSentToSqs={IsSentToSqs}",
                log.Sku, log.ValidationStatus, log.IsSentToSqs);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log item source to MySQL: Sku={Sku}, ValidationStatus={ValidationStatus}",
                log.Sku, log.ValidationStatus);
            return Result.Failure($"MySQL item source logging failed: {ex.Message}");
        }
    }
}