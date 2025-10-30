using System.Diagnostics.CodeAnalysis;
using ItemMaster.Contracts;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace ItemMaster.Application.Services;

public interface IItemPublishingService
{
    Task PublishItemsAsync(List<UnifiedItemMaster> unifiedItems, RequestSource requestSource, string traceId,
        CancellationToken cancellationToken);
}

[ExcludeFromCodeCoverage]
public class ItemPublishingService : IItemPublishingService
{
    private readonly IItemPublisher _itemPublisher;
    private readonly ILogger<ItemPublishingService> _logger;
    private readonly IObservabilityService _observabilityService;
    private readonly IItemMasterLogRepository _logRepository;

    public ItemPublishingService(
        IItemPublisher itemPublisher,
        IObservabilityService observabilityService,
        ILogger<ItemPublishingService> logger,
        IItemMasterLogRepository logRepository)
    {
        _itemPublisher = itemPublisher;
        _observabilityService = observabilityService;
        _logger = logger;
        _logRepository = logRepository;
    }

    public async Task PublishItemsAsync(List<UnifiedItemMaster> unifiedItems, RequestSource requestSource,
        string traceId, CancellationToken cancellationToken)
    {
        await _observabilityService.ExecuteWithObservabilityAsync(
            "PublishToSqs",
            requestSource,
            async () =>
            {
                var publishResult =
                    await _itemPublisher.PublishUnifiedItemsAsync(unifiedItems, traceId, cancellationToken);

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

                var publishedSkus = unifiedItems.Select(i => i.Sku).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (publishedSkus.Count > 0)
                {
                    var markResult = await _logRepository.MarkSentToSqsAsync(publishedSkus, cancellationToken);
                    if (!markResult.IsSuccess)
                        _logger.LogWarning("MarkSentToSqsAsync failed: {Error}", markResult.ErrorMessage);
                }

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