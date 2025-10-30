using ItemMaster.Shared;

namespace ItemMaster.Infrastructure.Observability;

public interface IMetricsService
{
    Task RecordProcessingMetricAsync(string operation, bool success, RequestSource requestSource,
        int? itemCount = null, TimeSpan? duration = null, CancellationToken cancellationToken = default);

    Task RecordCustomMetricAsync(string metricName, double value, string unit = "Count",
        Dictionary<string, string>? dimensions = null, CancellationToken cancellationToken = default);
}