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
    private string? _currentTraceId;

    public ProcessSkusUseCase(
        ISnowflakeRepository snowflakeRepository,
        IItemPublisher itemPublisher,
        IItemMasterLogRepository logRepository,
        ILogger<ProcessSkusUseCase> logger,
        IObservabilityService observabilityService)
    {
        _snowflakeRepository = snowflakeRepository;
        _itemPublisher = itemPublisher;
        _logRepository = logRepository;
        _logger = logger;
        _observabilityService = observabilityService;
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

                var simplifiedItems = itemsList.Select(item => new ItemForSqs
                {
                    Sku = item.Sku,
                    Brand = item.Brand,
                    Status = item.Status,
                    Region = item.Region,
                    Barcode = item.Barcode,
                    ProductTitle = item.ProductTitle,
                    Price = item.Price
                }).ToList();

                await _observabilityService.ExecuteWithObservabilityAsync(
                    "PublishToSqs",
                    requestSource,
                    async () =>
                    {
                        var publishResult =
                            await _itemPublisher.PublishSimplifiedItemsAsync(simplifiedItems.AsEnumerable(),
                                _currentTraceId, cancellationToken);

                        if (!publishResult.IsSuccess)
                        {
                            var errorMsg = $"Failed to publish items to SQS: {publishResult.ErrorMessage}";
                            _logger.LogError("SQS Publish failed: {Error} | TraceId: {TraceId}", errorMsg,
                                _currentTraceId);
                            await LogResultSafely("publish_to_sqs", false, requestSource, errorMsg,
                                simplifiedItems.Count);
                            throw new InvalidOperationException(errorMsg);
                        }

                        await LogResultSafely("publish_to_sqs", true, requestSource, null, simplifiedItems.Count);

                        // Record successful publish metrics
                        await _observabilityService.RecordMetricAsync("ItemsPublishedToSqs", simplifiedItems.Count,
                            new Dictionary<string, string>
                            {
                                ["requestSource"] = requestSource.ToString()
                            });

                        return publishResult;
                    },
                    new Dictionary<string, object>
                    {
                        ["itemCount"] = simplifiedItems.Count,
                        ["brands"] = simplifiedItems.Select(i => i.Brand).Distinct().Take(5).ToList()
                    },
                    cancellationToken);

                var response = new ProcessSkusResponse
                {
                    Success = true,
                    ItemsProcessed = itemsList.Count,
                    ItemsPublished = simplifiedItems.Count,
                    Failed = 0
                };

                _logger.LogInformation(
                    "ProcessSkus completed successfully | Processed: {Processed} | Published: {Published} | TraceId: {TraceId}",
                    response.ItemsProcessed, response.ItemsPublished, _currentTraceId);

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