using System.Diagnostics.CodeAnalysis;
using ItemMaster.Domain;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Application.Services;

public interface ISkuAnalysisService
{
    List<string> AnalyzeNotFoundSkus(List<string> requestedSkus, List<Item> itemsList);
}

[ExcludeFromCodeCoverage]
public class SkuAnalysisService : ISkuAnalysisService
{
    // Configuration constants
    private const string NOT_FOUND_MESSAGE_TEMPLATE = "SKUs not found in Snowflake: {NotFoundSkus}";

    private readonly ILogger<SkuAnalysisService> _logger;

    public SkuAnalysisService(ILogger<SkuAnalysisService> logger)
    {
        _logger = logger;
    }

    public List<string> AnalyzeNotFoundSkus(List<string> requestedSkus, List<Item> itemsList)
    {
        if (!requestedSkus.Any())
            return new List<string>();

        var foundSkus = CreateFoundSkusSet(itemsList);
        var notFoundSkus = FindMissingSkus(requestedSkus, foundSkus);

        LogNotFoundSkus(notFoundSkus);

        return notFoundSkus;
    }

    private static HashSet<string> CreateFoundSkusSet(List<Item> itemsList)
    {
        return itemsList.Select(i => i.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> FindMissingSkus(List<string> requestedSkus, HashSet<string> foundSkus)
    {
        return requestedSkus.Where(sku => !foundSkus.Contains(sku)).ToList();
    }

    private void LogNotFoundSkus(List<string> notFoundSkus)
    {
        if (notFoundSkus.Any())
        {
            var notFoundMessage = string.Join(", ", notFoundSkus);
            _logger.LogWarning(NOT_FOUND_MESSAGE_TEMPLATE + " | Count: {Count}",
                notFoundMessage, notFoundSkus.Count);
        }
    }
}