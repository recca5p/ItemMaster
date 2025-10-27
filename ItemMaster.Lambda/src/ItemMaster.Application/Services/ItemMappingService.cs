using System.Text.Json;
using ItemMaster.Contracts;
using ItemMaster.Domain;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application.Services;

public interface IItemMappingService
{
    Task<ItemMappingResult> MapItemsAsync(List<Item> items, RequestSource requestSource, string traceId);
}

public class ItemMappingService : IItemMappingService
{
    private readonly ILogger<ItemMappingService> _logger;
    private readonly IItemMasterLogRepository _logRepository;
    private readonly IObservabilityService _observabilityService;
    private readonly IUnifiedItemMapper _unifiedItemMapper;

    public ItemMappingService(
        IUnifiedItemMapper unifiedItemMapper,
        IItemMasterLogRepository logRepository,
        IObservabilityService observabilityService,
        ILogger<ItemMappingService> logger)
    {
        _unifiedItemMapper = unifiedItemMapper;
        _logRepository = logRepository;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    public async Task<ItemMappingResult> MapItemsAsync(List<Item> items, RequestSource requestSource, string traceId)
    {
        var result = new ItemMappingResult();

        foreach (var item in items)
        {
            var mappingResult = _unifiedItemMapper.MapToUnifiedModel(item);

            if (mappingResult.IsSuccess && mappingResult.UnifiedItem != null)
                await ProcessSuccessfulMapping(mappingResult, item, result, traceId);
            else
                await ProcessFailedMapping(mappingResult, item, result, traceId);
        }

        await LogMappingSummary(result, requestSource, traceId);
        return result;
    }

    private async Task ProcessSuccessfulMapping(MappingResult mappingResult, Item item, ItemMappingResult result,
        string traceId)
    {
        result.UnifiedItems.Add(mappingResult.UnifiedItem!);
        result.SuccessfulSkus.Add(mappingResult.Sku);

        result.PublishedItems.Add(new PublishedItemDetail
        {
            Sku = mappingResult.Sku,
            MappedItem = mappingResult.UnifiedItem,
            SkippedProperties = mappingResult.SkippedProperties
        });

        if (mappingResult.SkippedProperties.Any())
            _logger.LogWarning(
                "SKU_MAPPED_WITH_WARNINGS | SKU: {Sku} | SkippedProperties: {Properties} | TraceId: {TraceId}",
                mappingResult.Sku, string.Join(", ", mappingResult.SkippedProperties), traceId);

        await LogItemSourceSafely(new ItemMasterSourceLog
        {
            Sku = item.Sku,
            SourceModel = JsonSerializer.Serialize(item),
            ValidationStatus = "valid",
            CommonModel = JsonSerializer.Serialize(mappingResult.UnifiedItem),
            Errors = mappingResult.SkippedProperties.Any()
                ? $"Skipped optional properties: {string.Join(", ", mappingResult.SkippedProperties)}"
                : null,
            IsSentToSqs = false
        }, traceId);
    }

    private async Task ProcessFailedMapping(MappingResult mappingResult, Item item, ItemMappingResult result,
        string traceId)
    {
        var skippedItem = new SkippedItemDetail
        {
            Sku = mappingResult.Sku,
            Reason = "Validation failed",
            ValidationFailure = mappingResult.FailureReason ?? "Unknown validation error",
            AllValidationErrors = mappingResult.ValidationErrors
        };
        result.SkippedItems.Add(skippedItem);

        _logger.LogWarning(
            "SKU_SKIPPED | SKU: {Sku} | ErrorCount: {ErrorCount} | Errors: {Errors} | TraceId: {TraceId}",
            skippedItem.Sku, mappingResult.ValidationErrors.Count,
            string.Join("; ", mappingResult.ValidationErrors), traceId);

        await LogItemSourceSafely(new ItemMasterSourceLog
        {
            Sku = mappingResult.Sku,
            SourceModel = JsonSerializer.Serialize(item),
            ValidationStatus = "invalid",
            CommonModel = null,
            Errors = string.Join("; ", mappingResult.ValidationErrors),
            IsSentToSqs = false
        }, traceId);
    }

    private async Task LogMappingSummary(ItemMappingResult result, RequestSource requestSource, string traceId)
    {
        if (result.SkippedItems.Any())
        {
            _logger.LogWarning(
                "MAPPING_SUMMARY | Total Skipped: {SkippedCount} | Reasons: {Reasons} | TraceId: {TraceId}",
                result.SkippedItems.Count,
                string.Join("; ", result.SkippedItems.Select(s => $"{s.Sku}={s.ValidationFailure}").Take(10)),
                traceId);

            await _observabilityService.RecordMetricAsync("ItemsSkippedValidation", result.SkippedItems.Count,
                new Dictionary<string, string>
                {
                    ["requestSource"] = requestSource.ToString()
                });
        }
    }

    private async Task LogItemSourceSafely(ItemMasterSourceLog log, string traceId)
    {
        try
        {
            await _logRepository.LogItemSourceAsync(log, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log item source to MySQL repository | Sku: {Sku} | TraceId: {TraceId}",
                log.Sku, traceId);
        }
    }
}