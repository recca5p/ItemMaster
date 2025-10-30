using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;

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

    public async Task<Result> MarkSentToSqsAsync(IEnumerable<string> skus, CancellationToken cancellationToken = default)
    {
        try
        {
            var skuSet = new HashSet<string>(skus.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
            if (skuSet.Count == 0) return Result.Success();

            var affected = 0;
            foreach (var sku in skuSet)
            {
                var latest = await _db.ItemMasterSourceLogs
                    .Where(l => l.Sku == sku)
                    .OrderByDescending(l => l.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latest != null && !latest.IsSentToSqs)
                {
                    latest.IsSentToSqs = true;
                    affected++;
                }
            }

            if (affected > 0)
                await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Marked {Count} log entries as sent to SQS", affected);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark logs as sent to SQS");
            return Result.Failure($"MarkSentToSqs failed: {ex.Message}");
        }
    }
}