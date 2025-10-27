using ItemMaster.Shared;

namespace ItemMaster.Infrastructure.Observability;

public class InMemoryMetricsService : IMetricsService
{
    private readonly List<MetricRecord> _metrics = new();

    public Task RecordProcessingMetricAsync(string operation, bool success, RequestSource requestSource,
        int? itemCount = null, TimeSpan? duration = null, CancellationToken cancellationToken = default)
    {
        _metrics.Add(new MetricRecord
        {
            Operation = operation,
            Success = success,
            RequestSource = requestSource,
            ItemCount = itemCount,
            Duration = duration,
            Timestamp = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public Task RecordCustomMetricAsync(string metricName, double value, string unit = "Count",
        Dictionary<string, string>? dimensions = null, CancellationToken cancellationToken = default)
    {
        _metrics.Add(new MetricRecord
        {
            MetricName = metricName,
            Value = value,
            Unit = unit,
            Dimensions = dimensions,
            Timestamp = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public IReadOnlyList<MetricRecord> GetMetrics()
    {
        return _metrics.AsReadOnly();
    }

    public void Clear()
    {
        _metrics.Clear();
    }
}