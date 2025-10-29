using System.Diagnostics.CodeAnalysis;
using ItemMaster.Contracts;
using ItemMaster.Domain;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application.Services;

public interface ISkuProcessingOrchestrator
{
    Task<Result<ProcessSkusResponse>> ProcessSkusAsync(ProcessSkusRequest request, RequestSource requestSource,
        string traceId, CancellationToken cancellationToken);
}

[ExcludeFromCodeCoverage]
public class SkuProcessingOrchestrator : ISkuProcessingOrchestrator
{
    // Configuration constants
    private const int DEFAULT_LATEST_ITEMS_LIMIT = 100;
    private const int MAX_BATCH_SIZE = 1000;
    private const int TIMEOUT_SECONDS = 300;

    private const string HEALTH_CHECK_LOG_MESSAGE =
        "Health check request with no SKUs - returning success without processing | TraceId: {TraceId}";

    private readonly IItemFetchingService _itemFetchingService;
    private readonly IItemMappingService _itemMappingService;
    private readonly IItemPublishingService _itemPublishingService;
    private readonly ILogger<SkuProcessingOrchestrator> _logger;
    private readonly IObservabilityService _observabilityService;
    private readonly IProcessingResponseBuilder _responseBuilder;
    private readonly ISkuAnalysisService _skuAnalysisService;

    public SkuProcessingOrchestrator(
        IItemFetchingService itemFetchingService,
        IItemMappingService itemMappingService,
        IItemPublishingService itemPublishingService,
        ISkuAnalysisService skuAnalysisService,
        IProcessingResponseBuilder responseBuilder,
        IObservabilityService observabilityService,
        ILogger<SkuProcessingOrchestrator> logger)
    {
        _itemFetchingService = itemFetchingService;
        _itemMappingService = itemMappingService;
        _itemPublishingService = itemPublishingService;
        _skuAnalysisService = skuAnalysisService;
        _responseBuilder = responseBuilder;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    public async Task<Result<ProcessSkusResponse>> ProcessSkusAsync(
        ProcessSkusRequest request,
        RequestSource requestSource,
        string traceId,
        CancellationToken cancellationToken)
    {
        var requestedSkus = request.GetAllSkus();

        if (IsHealthCheckRequest(requestedSkus, requestSource)) return CreateHealthCheckResponse(traceId);

        return await _observabilityService.ExecuteWithObservabilityAsync(
            "ProcessSkusUseCase",
            requestSource,
            async () => await ExecuteProcessingWorkflow(requestedSkus, requestSource, traceId, cancellationToken),
            CreateProcessingMetadata(requestedSkus),
            cancellationToken);
    }

    private static bool IsHealthCheckRequest(List<string> requestedSkus, RequestSource requestSource)
    {
        return !requestedSkus.Any() && requestSource == RequestSource.CicdHealthCheck;
    }

    private Result<ProcessSkusResponse> CreateHealthCheckResponse(string traceId)
    {
        _logger.LogInformation(HEALTH_CHECK_LOG_MESSAGE, traceId);

        var healthCheckResponse = new ProcessSkusResponse
        {
            Success = true,
            ItemsProcessed = 0,
            ItemsPublished = 0,
            Failed = 0
        };

        return Result<ProcessSkusResponse>.Success(healthCheckResponse);
    }

    private async Task<Result<ProcessSkusResponse>> ExecuteProcessingWorkflow(
        List<string> requestedSkus,
        RequestSource requestSource,
        string traceId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing SKUs request | Count: {SkuCount} | RequestSource: {RequestSource} | TraceId: {TraceId}",
            requestedSkus.Count, requestSource, traceId);

        // Step 1: Fetch items
        var itemsList = await FetchItemsAsync(requestedSkus, requestSource, traceId, cancellationToken);

        // Step 2: Analyze not found SKUs
        var notFoundSkus = _skuAnalysisService.AnalyzeNotFoundSkus(requestedSkus, itemsList);

        // Step 3: Map items to unified format
        var mappingResult = await _itemMappingService.MapItemsAsync(itemsList, requestSource, traceId);

        // Step 4: Log processing summary
        LogProcessingSummary(itemsList, notFoundSkus, mappingResult, traceId);

        // Step 5: Publish items
        await _itemPublishingService.PublishItemsAsync(mappingResult.UnifiedItems, requestSource, traceId,
            cancellationToken);

        // Step 6: Create response
        var response = _responseBuilder.CreateSuccessResponse(itemsList, notFoundSkus, mappingResult);

        _logger.LogInformation(
            "ProcessSkus completed successfully | Processed: {Processed} | Published: {Published} | Failed: {Failed} | NotFound: {NotFound} | TraceId: {TraceId}",
            response.ItemsProcessed, response.ItemsPublished, response.Failed, notFoundSkus.Count, traceId);

        return Result<ProcessSkusResponse>.Success(response);
    }

    private async Task<List<Item>> FetchItemsAsync(
        List<string> requestedSkus,
        RequestSource requestSource,
        string traceId,
        CancellationToken cancellationToken)
    {
        if (requestedSkus.Any())
            return await _itemFetchingService.FetchItemsBySkusAsync(requestedSkus, requestSource, traceId,
                cancellationToken);

        var limit = DEFAULT_LATEST_ITEMS_LIMIT;
        return await _itemFetchingService.FetchLatestItemsAsync(limit, requestSource, traceId, cancellationToken);
    }

    private void LogProcessingSummary(List<Item> itemsList, List<string> notFoundSkus, ItemMappingResult mappingResult,
        string traceId)
    {
        _logger.LogInformation(
            "PROCESSING_COMPLETE | Found: {Found} | NotFound: {NotFound} | Mapped: {Mapped} | Skipped: {Skipped} | TraceId: {TraceId}",
            itemsList.Count, notFoundSkus.Count, mappingResult.UnifiedItems.Count, mappingResult.SkippedItems.Count,
            traceId);
    }

    private static Dictionary<string, object> CreateProcessingMetadata(List<string> requestedSkus)
    {
        return new Dictionary<string, object>
        {
            ["inputSkuCount"] = requestedSkus.Count,
            ["hasSpecificSkus"] = requestedSkus.Any()
        };
    }
}