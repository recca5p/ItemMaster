using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using ItemMaster.Shared;

namespace ItemMaster.Infrastructure.Observability;

public interface IMetricsService
{
    Task RecordProcessingMetricAsync(string operation, bool success, RequestSource requestSource,
        int? itemCount = null, TimeSpan? duration = null, CancellationToken cancellationToken = default);

    Task RecordCustomMetricAsync(string metricName, double value, string unit = "Count",
        Dictionary<string, string>? dimensions = null, CancellationToken cancellationToken = default);
}

public class CloudWatchMetricsService : IMetricsService
{
    private readonly IAmazonCloudWatch _cloudWatch;
    private readonly string _namespace;

    public CloudWatchMetricsService(IAmazonCloudWatch cloudWatch)
    {
        _cloudWatch = cloudWatch;
        _namespace = "ItemMaster/Lambda";
    }

    public async Task RecordProcessingMetricAsync(string operation, bool success, RequestSource requestSource,
        int? itemCount = null, TimeSpan? duration = null, CancellationToken cancellationToken = default)
    {
        var dimensions = new List<Dimension>
        {
            new() { Name = "Operation", Value = operation },
            new() { Name = "RequestSource", Value = requestSource.ToString() }
        };

        var metricData = new List<MetricDatum>
        {
            new()
            {
                MetricName = success ? "ProcessingSuccess" : "ProcessingFailure",
                Value = 1,
                Unit = StandardUnit.Count,
                Dimensions = dimensions,
                TimestampUtc = DateTime.UtcNow
            }
        };

        if (itemCount.HasValue)
            metricData.Add(new MetricDatum
            {
                MetricName = "ItemsProcessed",
                Value = itemCount.Value,
                Unit = StandardUnit.Count,
                Dimensions = dimensions,
                TimestampUtc = DateTime.UtcNow
            });

        if (duration.HasValue)
            metricData.Add(new MetricDatum
            {
                MetricName = "ProcessingDuration",
                Value = duration.Value.TotalMilliseconds,
                Unit = StandardUnit.Milliseconds,
                Dimensions = dimensions,
                TimestampUtc = DateTime.UtcNow
            });

        await _cloudWatch.PutMetricDataAsync(new PutMetricDataRequest
        {
            Namespace = _namespace,
            MetricData = metricData
        }, cancellationToken);
    }

    public async Task RecordCustomMetricAsync(string metricName, double value, string unit = "Count",
        Dictionary<string, string>? dimensions = null, CancellationToken cancellationToken = default)
    {
        var metricDimensions = dimensions?.Select(kv => new Dimension
        {
            Name = kv.Key,
            Value = kv.Value
        }).ToList() ?? new List<Dimension>();

        await _cloudWatch.PutMetricDataAsync(new PutMetricDataRequest
        {
            Namespace = _namespace,
            MetricData = new List<MetricDatum>
            {
                new()
                {
                    MetricName = metricName,
                    Value = value,
                    Unit = unit,
                    Dimensions = metricDimensions,
                    TimestampUtc = DateTime.UtcNow
                }
            }
        }, cancellationToken);
    }
}

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