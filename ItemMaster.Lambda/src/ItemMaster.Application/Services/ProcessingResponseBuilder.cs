using System.Diagnostics.CodeAnalysis;
using ItemMaster.Contracts;
using ItemMaster.Domain;

namespace ItemMaster.Application.Services;

public interface IProcessingResponseBuilder
{
    ProcessSkusResponse CreateSuccessResponse(
        List<Item> itemsList,
        List<string> notFoundSkus,
        ItemMappingResult mappingResult);
}

[ExcludeFromCodeCoverage]
public class ProcessingResponseBuilder : IProcessingResponseBuilder
{
    public ProcessSkusResponse CreateSuccessResponse(
        List<Item> itemsList,
        List<string> notFoundSkus,
        ItemMappingResult mappingResult)
    {
        return new ProcessSkusResponse
        {
            Success = true,
            ItemsProcessed = itemsList.Count,
            ItemsPublished = mappingResult.UnifiedItems.Count,
            Failed = mappingResult.SkippedItems.Count,
            SkusNotFound = notFoundSkus,
            SkippedItems = mappingResult.SkippedItems,
            SuccessfulSkus = mappingResult.SuccessfulSkus,
            PublishedItems = mappingResult.PublishedItems
        };
    }
}