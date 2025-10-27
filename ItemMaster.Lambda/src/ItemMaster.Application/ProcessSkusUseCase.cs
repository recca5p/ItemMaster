using ItemMaster.Application.Services;
using ItemMaster.Contracts;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application;

public class ProcessSkusUseCase : IProcessSkusUseCase
{
    // Configuration constants
    private const string USE_CASE_NAME = "ProcessSkusUseCase";
    private readonly ILogger<ProcessSkusUseCase> _logger;

    private readonly ISkuProcessingOrchestrator _orchestrator;

    public ProcessSkusUseCase(
        ISkuProcessingOrchestrator orchestrator,
        ILogger<ProcessSkusUseCase> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<Result<ProcessSkusResponse>> ExecuteAsync(
        ProcessSkusRequest request,
        RequestSource requestSource = RequestSource.Unknown,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentTraceId = traceId ?? Guid.NewGuid().ToString("N");

        _logger.LogDebug("{UseCase} started | RequestSource: {RequestSource} | TraceId: {TraceId}",
            USE_CASE_NAME, requestSource, currentTraceId);

        try
        {
            return await _orchestrator.ProcessSkusAsync(request, requestSource, currentTraceId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{UseCase} failed | TraceId: {TraceId}", USE_CASE_NAME, currentTraceId);
            return Result<ProcessSkusResponse>.Failure($"Processing failed: {ex.Message}");
        }
    }
}