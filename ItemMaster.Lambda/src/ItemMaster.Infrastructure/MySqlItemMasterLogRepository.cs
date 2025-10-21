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


    public async Task<Result> LogItemSourceAsync(ItemMasterSourceLog log, CancellationToken cancellationToken = default)
    {
        try
        {
            log.CreatedAt = _clock.UtcNow;
            _context.ItemMasterSourceLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log item source to MySQL: Sku={Sku}, ValidationStatus={ValidationStatus}, IsSentToSqs={IsSentToSqs}",
                log.Sku, log.ValidationStatus, log.IsSentToSqs);
            return Result.Failure($"MySQL item source logging failed: {ex.Message}");
        }
    }
}