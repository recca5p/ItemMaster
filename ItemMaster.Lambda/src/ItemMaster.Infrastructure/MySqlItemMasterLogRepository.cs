using ItemMaster.Infrastructure.Ef;
using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;

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

    public async Task<Result> MarkSentToSqsAsync(IEnumerable<string> skus, CancellationToken cancellationToken = default)
    {
        try
        {
            var skuSet = new HashSet<string>(skus.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
            if (skuSet.Count == 0) return Result.Success();

            var affected = 0;
            foreach (var sku in skuSet)
            {
                var latest = await _context.ItemMasterSourceLogs
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
                await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark logs as sent to SQS");
            return Result.Failure($"MarkSentToSqs failed: {ex.Message}");
        }
    }
}