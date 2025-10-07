using ItemMaster.Contracts;
using ItemMaster.Domain;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application;

public sealed class ProcessSkusUseCase : IProcessSkusUseCase
{
    private readonly IItemMasterLogRepository _logRepository;
    private readonly IClock _clock;
    private readonly ILogger<ProcessSkusUseCase> _logger;
    private readonly IItemPublisher _publisher;

    public ProcessSkusUseCase(IItemMasterLogRepository logRepository, IClock clock, ILogger<ProcessSkusUseCase> logger, IItemPublisher publisher)
    {
        _logRepository = logRepository;
        _clock = clock;
        _logger = logger;
        _publisher = publisher;
    }

    public async Task<ProcessSkusResponse> ExecuteAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken ct = default)
    {
        var skuList = skus.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => new Item(s).Sku).ToList();
        int published = 0;
        if (skuList.Count > 0)
        {
            try { published = await _publisher.PublishAsync(skuList, source, requestId, ct); }
            catch (Exception ex) { _logger.LogError(ex, "PublishFailure requestId={RequestId} count={Count}", requestId, skuList.Count); }
        }
        var timestamp = _clock.UtcNow;
        var logRecords = skuList.Select(s => new ItemLogRecord(s, source, requestId, timestamp)).ToList();
        int logged = 0;
        if (logRecords.Count > 0)
        {
            try { logged = await _logRepository.InsertLogsAsync(logRecords, ct); }
            catch (Exception ex) { _logger.LogError(ex, "LogInsertFailure requestId={RequestId} count={Count}", requestId, logRecords.Count); }
        }
        return new ProcessSkusResponse { Published = published, Logged = logged, Failed = skuList.Count - published };
    }
}
