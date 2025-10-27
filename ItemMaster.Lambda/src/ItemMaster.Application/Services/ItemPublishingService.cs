using ItemMaster.Contracts;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application.Services;

public interface IItemPublishingService
{
    Task PublishItemsAsync(List<UnifiedItemMaster> unifiedItems, RequestSource requestSource, string traceId, CancellationToken cancellationToken);
}

public class ItemPublishingService : IItemPublishingService
{
    private readonly IItemPublisher _itemPublisher;
    private readonly IObservabilityService _observabilityService;
    private readonly ILogger<ItemPublishingService> _logger;

    public ItemPublishingService(
        IItemPublisher itemPublisher,
        IObservabilityService observabilityService,
        ILogger<ItemPublishingService> logger)
    {
        _itemPublisher = itemPublisher;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    public async Task PublishItemsAsync(List<UnifiedItemMaster> unifiedItems, RequestSource requestSource, string traceId, CancellationToken cancellationToken)
    {
        await _observabilityService.ExecuteWithObservabilityAsync(
            "PublishToSqs",
            requestSource,
            async () =>
            {
                var publishResult = await _itemPublisher.PublishUnifiedItemsAsync(unifiedItems, traceId, cancellationToken);

                if (!publishResult.IsSuccess)
                {
                    var errorMsg = $"Failed to publish items to SQS: {publishResult.ErrorMessage}";
                    _logger.LogError("SQS Publish failed: {Error} | TraceId: {TraceId}", errorMsg, traceId);
                    throw new InvalidOperationException(errorMsg);
                }

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
    }
}
