using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ItemMaster.Contracts;

[ExcludeFromCodeCoverage]
public class ProcessSkusRequest
{
    public List<string> Skus { get; set; } = new();

    // Optional: Accept SKUs as comma-separated string
    public string? SkusString { get; set; }

    /// <summary>
    ///     Gets all SKUs from both Skus list and SkusString, handling comma-separated values
    /// </summary>
    public List<string> GetAllSkus()
    {
        var allSkus = new List<string>(Skus);

        if (!string.IsNullOrWhiteSpace(SkusString))
            try
            {
                var jsonArray = JsonSerializer.Deserialize<string[]>(SkusString);
                if (jsonArray != null)
                    allSkus.AddRange(jsonArray.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            catch (JsonException)
            {
                var commaSeparated = SkusString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                allSkus.AddRange(commaSeparated);
            }

        return allSkus.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}