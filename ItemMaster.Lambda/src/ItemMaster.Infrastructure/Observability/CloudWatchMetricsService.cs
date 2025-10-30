using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using ItemMaster.Shared;

namespace ItemMaster.Infrastructure.Observability;

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