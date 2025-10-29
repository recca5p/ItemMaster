using System.Diagnostics.CodeAnalysis;
using ItemMaster.Domain;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application.Services;

public interface IItemFetchingService
{
    Task<List<Item>> FetchItemsBySkusAsync(List<string> skus, RequestSource requestSource, string traceId,
        CancellationToken cancellationToken);

    Task<List<Item>> FetchLatestItemsAsync(int limit, RequestSource requestSource, string traceId,
        CancellationToken cancellationToken);
}

[ExcludeFromCodeCoverage]
public class ItemFetchingService : IItemFetchingService
{
    private readonly ILogger<ItemFetchingService> _logger;
    private readonly IObservabilityService _observabilityService;
    private readonly ISnowflakeRepository _snowflakeRepository;

    public ItemFetchingService(
        ISnowflakeRepository snowflakeRepository,
        IObservabilityService observabilityService,
        ILogger<ItemFetchingService> logger)
    {
        _snowflakeRepository = snowflakeRepository;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    public async Task<List<Item>> FetchItemsBySkusAsync(List<string> skus, RequestSource requestSource, string traceId,
        CancellationToken cancellationToken)
    {
        return await _observabilityService.ExecuteWithObservabilityAsync(
            "FetchItemsBySkus",
            requestSource,
            async () =>
            {
                var itemsResult = await _snowflakeRepository.GetItemsBySkusAsync(skus, cancellationToken);

                if (!itemsResult.IsSuccess)
                {
                    var errorMsg = $"Failed to fetch items from Snowflake for SKUs: {itemsResult.ErrorMessage}";
                    _logger.LogError("{Error} | TraceId: {TraceId}", errorMsg, traceId);
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
                ["skuCount"] = skus.Count,
                ["skuList"] = string.Join(",", skus.Take(10))
            },
            cancellationToken);
    }

    public async Task<List<Item>> FetchLatestItemsAsync(int limit, RequestSource requestSource, string traceId,
        CancellationToken cancellationToken)
    {
        return await _observabilityService.ExecuteWithObservabilityAsync(
            "FetchLatestItems",
            requestSource,
            async () =>
            {
                var latestItemsResult = await _snowflakeRepository.GetLatestItemsAsync(limit, cancellationToken);

                if (!latestItemsResult.IsSuccess)
                {
                    var errorMsg = $"Failed to fetch latest items from Snowflake: {latestItemsResult.ErrorMessage}";
                    _logger.LogError("{Error} | TraceId: {TraceId}", errorMsg, traceId);
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
    }
}