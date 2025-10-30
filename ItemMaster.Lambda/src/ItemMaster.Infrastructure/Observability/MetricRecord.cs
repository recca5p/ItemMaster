using System.Diagnostics.CodeAnalysis;
using ItemMaster.Shared;

namespace ItemMaster.Infrastructure.Observability;

[ExcludeFromCodeCoverage]
public class MetricRecord
{
    public string? Operation { get; set; }
    public bool Success { get; set; }
    public RequestSource RequestSource { get; set; }
    public int? ItemCount { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? MetricName { get; set; }
    public double Value { get; set; }
    public string? Unit { get; set; }
    public Dictionary<string, string>? Dimensions { get; set; }
    public DateTime Timestamp { get; set; }
}