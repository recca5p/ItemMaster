using ItemMaster.Contracts;

namespace ItemMaster.Application.Services;

public interface IProcessingResponseBuilder
{
    ProcessSkusResponse CreateSuccessResponse(
        List<Domain.Item> itemsList, 
        List<string> notFoundSkus, 
        ItemMappingResult mappingResult);
}

public class ProcessingResponseBuilder : IProcessingResponseBuilder
{
    public ProcessSkusResponse CreateSuccessResponse(
        List<Domain.Item> itemsList, 
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
