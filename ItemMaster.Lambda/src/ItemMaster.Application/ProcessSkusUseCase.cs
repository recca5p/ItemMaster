using ItemMaster.Contracts;
using ItemMaster.Domain;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application;

public interface IProcessSkusUseCase
{
    Task<ProcessSkusResponse> ExecuteAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken ct = default);
}

public sealed class ProcessSkusUseCase : IProcessSkusUseCase
{
    private readonly IItemMasterLogRepository _logRepository;
    private readonly IClock _clock;
    private readonly ILogger<ProcessSkusUseCase> _logger;

    public ProcessSkusUseCase(IItemMasterLogRepository logRepository, IClock clock, ILogger<ProcessSkusUseCase> logger)
    {
        _logRepository = logRepository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ProcessSkusResponse> ExecuteAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken ct = default)
    {
        // var list = skus.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => new Item(s)).ToList();
        var list = skus.ToList();

        var timestamp = _clock.UtcNow;
        var logRecords = list.Select(i => new ItemLogRecord(i, source, requestId, timestamp)).ToList();

        int logged = 0;
        if (logRecords.Count > 0)
        {
            try
            {
                logged = await _logRepository.InsertLogsAsync(logRecords, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SkuProcessingInsertFailure requestId={RequestId} attempted={Attempted}", requestId, logRecords.Count);
            }
        }

        return new ProcessSkusResponse { Published = 0, Logged = logged, Failed = list.Count - logged };
    }
}
