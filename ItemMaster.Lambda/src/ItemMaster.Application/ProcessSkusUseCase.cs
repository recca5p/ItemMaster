using ItemMaster.Contracts;
using ItemMaster.Domain;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application;

public class ProcessSkusUseCase : IProcessSkusUseCase
{
    private readonly IItemPublisher _itemPublisher;
    private readonly ILogger<ProcessSkusUseCase> _logger;
    private readonly IItemMasterLogRepository _logRepository;
    private readonly IObservabilityService _observabilityService;
    private readonly ISnowflakeRepository _snowflakeRepository;
    private readonly IUnifiedItemMapper _unifiedItemMapper;
    private string? _currentTraceId;

    public ProcessSkusUseCase(
        ISnowflakeRepository snowflakeRepository,
        IItemPublisher itemPublisher,
        IItemMasterLogRepository logRepository,
        ILogger<ProcessSkusUseCase> logger,
        IObservabilityService observabilityService,
        IUnifiedItemMapper unifiedItemMapper)
    {
        _snowflakeRepository = snowflakeRepository;
        _itemPublisher = itemPublisher;
        _logRepository = logRepository;
        _logger = logger;
        _observabilityService = observabilityService;
        _unifiedItemMapper = unifiedItemMapper;
    }

    public async Task<Result<ProcessSkusResponse>> ExecuteAsync(ProcessSkusRequest request,
        RequestSource requestSource = RequestSource.Unknown,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        _currentTraceId = traceId ?? _observabilityService.GetCurrentTraceId();

        var requestedSkus = request.GetAllSkus();

        if (!requestedSkus.Any() && requestSource == RequestSource.CicdHealthCheck)
        {
            _logger.LogInformation(
                "Health check request with no SKUs - returning success without processing | TraceId: {TraceId}",
                _currentTraceId);

            var healthCheckResponse = new ProcessSkusResponse
            {
                Success = true,
                ItemsProcessed = 0,
                ItemsPublished = 0,
                Failed = 0
            };

            return Result<ProcessSkusResponse>.Success(healthCheckResponse);
        }

        return await _observabilityService.ExecuteWithObservabilityAsync(
            "ProcessSkusUseCase",
            requestSource,
            async () =>
            {
                _logger.LogInformation(
                    "Processing SKUs request | Count: {SkuCount} | RequestSource: {RequestSource} | TraceId: {TraceId}",
                    requestedSkus.Count, requestSource, _currentTraceId);

                List<Item> itemsList;

                if (requestedSkus.Any())
                    itemsList = await _observabilityService.ExecuteWithObservabilityAsync(
                        "FetchItemsBySkus",
                        requestSource,
                        async () =>
                        {
                            var itemsResult =
                                await _snowflakeRepository.GetItemsBySkusAsync(requestedSkus, cancellationToken);

                            if (!itemsResult.IsSuccess)
                            {
                                var errorMsg =
                                    $"Failed to fetch items from Snowflake for SKUs: {itemsResult.ErrorMessage}";
                                _logger.LogError("{Error} | TraceId: {TraceId}", errorMsg, _currentTraceId);
                                await LogResultSafely("fetch_by_skus", false, requestSource, errorMsg,
                                    requestedSkus.Count);
                                throw new InvalidOperationException(errorMsg);
                            }

                            var items = (itemsResult.Value ?? Enumerable.Empty<Item>()).ToList();

                            await _observabilityService.RecordMetricAsync("SnowflakeItemsFetched", items.Count,
                                new Dictionary<string, string>
                                {
                                    ["operation"] = "fetch_by_skus",
                                    ["requestSource"] = requestSource.ToString()
                                });

                            return items;
                        },
                        new Dictionary<string, object>
                        {
                            ["skuCount"] = requestedSkus.Count,
                            ["skuList"] = string.Join(",", requestedSkus.Take(10))
                        },
                        cancellationToken);
                else
                    itemsList = await _observabilityService.ExecuteWithObservabilityAsync(
                        "FetchLatestItems",
                        requestSource,
                        async () =>
                        {
                            var latestItemsResult =
                                await _snowflakeRepository.GetLatestItemsAsync(100, cancellationToken);

                            if (!latestItemsResult.IsSuccess)
                            {
                                var errorMsg =
                                    $"Failed to fetch latest items from Snowflake: {latestItemsResult.ErrorMessage}";
                                _logger.LogError("{Error} | TraceId: {TraceId}", errorMsg, _currentTraceId);
                                await LogResultSafely("fetch_latest", false, requestSource, errorMsg);
                                throw new InvalidOperationException(errorMsg);
                            }

                            var items = (latestItemsResult.Value ?? Enumerable.Empty<Item>()).ToList();

                            await _observabilityService.RecordMetricAsync("LatestItemsFetched", items.Count,
                                new Dictionary<string, string>
                                {
                                    ["operation"] = "fetch_latest",
                                    ["requestSource"] = requestSource.ToString()
                                });

                            return items;
                        },
                        cancellationToken: cancellationToken);

                var foundSkus = itemsList.Select(i => i.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var notFoundSkus = requestedSkus.Any()
                    ? requestedSkus.Where(sku => !foundSkus.Contains(sku)).ToList()
                    : new List<string>();

                if (notFoundSkus.Any())
                {
                    var notFoundMessage = $"SKUs not found in Snowflake: {string.Join(", ", notFoundSkus)}";
                    _logger.LogWarning("{Message} | Count: {Count} | TraceId: {TraceId}",
                        notFoundMessage, notFoundSkus.Count, _currentTraceId);

                    await LogResultSafely("skus_not_found", true, requestSource, notFoundMessage, notFoundSkus.Count);
                }

                var unifiedItems = new List<UnifiedItemMaster>();
                var skippedItems = new List<SkippedItemDetail>();
                var successfulSkus = new List<string>();

                foreach (var item in itemsList)
                {
                    var mappingResult = _unifiedItemMapper.MapToUnifiedModel(item);
                    if (mappingResult.IsSuccess && mappingResult.UnifiedItem != null)
                    {
                        unifiedItems.Add(mappingResult.UnifiedItem);
                        successfulSkus.Add(mappingResult.Sku);
                    }
                    else
                    {
                        var skippedItem = new SkippedItemDetail
                        {
                            Sku = mappingResult.Sku,
                            Reason = "Validation failed",
                            ValidationFailure = mappingResult.FailureReason ?? "Unknown validation error"
                        };
                        skippedItems.Add(skippedItem);

                        _logger.LogWarning(
                            "SKU_SKIPPED | SKU: {Sku} | Reason: {Reason} | TraceId: {TraceId}",
                            skippedItem.Sku, skippedItem.ValidationFailure, _currentTraceId);

                        await LogResultSafely("sku_mapping_failed", true, requestSource,
                            $"SKU: {skippedItem.Sku} | Reason: {skippedItem.ValidationFailure}", 1);
                    }
                }

                if (skippedItems.Any())
                {
                    _logger.LogWarning(
                        "MAPPING_SUMMARY | Total Skipped: {SkippedCount} | Reasons: {Reasons} | TraceId: {TraceId}",
                        skippedItems.Count,
                        string.Join("; ", skippedItems.Select(s => $"{s.Sku}={s.ValidationFailure}").Take(10)),
                        _currentTraceId);

                    await _observabilityService.RecordMetricAsync("ItemsSkippedValidation", skippedItems.Count,
                        new Dictionary<string, string>
                        {
                            ["requestSource"] = requestSource.ToString()
                        });
                }

                _logger.LogInformation(
                    "PROCESSING_COMPLETE | Found: {Found} | NotFound: {NotFound} | Mapped: {Mapped} | Skipped: {Skipped} | TraceId: {TraceId}",
                    itemsList.Count, notFoundSkus.Count, unifiedItems.Count, skippedItems.Count, _currentTraceId);

                await _observabilityService.ExecuteWithObservabilityAsync(
                    "PublishToSqs",
                    requestSource,
                    async () =>
                    {
                        var publishResult =
                            await _itemPublisher.PublishUnifiedItemsAsync(unifiedItems,
                                _currentTraceId, cancellationToken);

                        if (!publishResult.IsSuccess)
                        {
                            var errorMsg = $"Failed to publish items to SQS: {publishResult.ErrorMessage}";
                            _logger.LogError("SQS Publish failed: {Error} | TraceId: {TraceId}", errorMsg,
                                _currentTraceId);
                            await LogResultSafely("publish_to_sqs", false, requestSource, errorMsg,
                                unifiedItems.Count);
                            throw new InvalidOperationException(errorMsg);
                        }

                        await LogResultSafely("publish_to_sqs", true, requestSource, null, unifiedItems.Count);

                        await _observabilityService.RecordMetricAsync("ItemsPublishedToSqs", unifiedItems.Count,
                            new Dictionary<string, string>
                            {
                                ["requestSource"] = requestSource.ToString()
                            });

                        return publishResult;
                    },
                    new Dictionary<string, object>
                    {
                        ["itemCount"] = unifiedItems.Count,
                        ["brands"] = unifiedItems
                            .SelectMany(i => i.Attributes.Where(a => a.Id == "brand_entity").Select(a => a.Value))
                            .Distinct().Take(5).ToList()
                    },
                    cancellationToken);

                var response = new ProcessSkusResponse
                {
                    Success = true,
                    ItemsProcessed = itemsList.Count,
                    ItemsPublished = unifiedItems.Count,
                    Failed = skippedItems.Count,
                    SkusNotFound = notFoundSkus,
                    SkippedItems = skippedItems,
                    SuccessfulSkus = successfulSkus
                };

                _logger.LogInformation(
                    "ProcessSkus completed successfully | Processed: {Processed} | Published: {Published} | Failed: {Failed} | NotFound: {NotFound} | TraceId: {TraceId}",
                    response.ItemsProcessed, response.ItemsPublished, response.Failed, notFoundSkus.Count,
                    _currentTraceId);

                return Result<ProcessSkusResponse>.Success(response);
            },
            new Dictionary<string, object>
            {
                ["inputSkuCount"] = request.GetAllSkus().Count,
                ["hasSpecificSkus"] = request.GetAllSkus().Any()
            },
            cancellationToken);
    }

    private async Task LogResultSafely(string operation, bool success, RequestSource requestSource,
        string? errorMessage, int? itemCount = null)
    {
        try
        {
            await _logRepository.LogProcessingResultAsync(operation, success, requestSource, errorMessage, itemCount,
                _currentTraceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log to MySQL repository | Operation: {Operation} | TraceId: {TraceId}",
                operation, _currentTraceId);
        }
    }
}