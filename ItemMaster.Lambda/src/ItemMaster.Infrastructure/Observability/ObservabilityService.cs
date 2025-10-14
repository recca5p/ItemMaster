using System.Diagnostics;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure.Observability;

public interface IObservabilityService
{
    Task<T> ExecuteWithObservabilityAsync<T>(
        string operationName,
        RequestSource requestSource,
        Func<Task<T>> operation,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    void LogWithTraceId(LogLevel logLevel, string message, params object[] args);
    string? GetCurrentTraceId();
    Task RecordMetricAsync(string metricName, double value, Dictionary<string, string>? dimensions = null);
}

public class ObservabilityService : IObservabilityService
{
    private readonly ILogger<ObservabilityService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly ITracingService _tracingService;

    public ObservabilityService(
        ILogger<ObservabilityService> logger,
        IMetricsService metricsService,
        ITracingService tracingService)
    {
        _logger = logger;
        _metricsService = metricsService;
        _tracingService = tracingService;
    }

    public async Task<T> ExecuteWithObservabilityAsync<T>(
        string operationName,
        RequestSource requestSource,
        Func<Task<T>> operation,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var traceId = _tracingService.GetCurrentTraceId();

        _tracingService.AddAnnotation("operation", operationName);
        _tracingService.AddAnnotation("requestSource", requestSource.ToString());
        _tracingService.AddAnnotation("traceId", traceId ?? "unknown");

        if (metadata != null)
            foreach (var kvp in metadata)
                _tracingService.AddMetadata("itemmaster", kvp.Key, kvp.Value);

        _logger.LogInformation("Starting operation: {Operation} | RequestSource: {RequestSource} | TraceId: {TraceId}",
            operationName, requestSource, traceId);

        try
        {
            using var subsegment = _tracingService.BeginSubsegment(operationName);

            var result = await operation();
            stopwatch.Stop();

            await _metricsService.RecordProcessingMetricAsync(
                operationName,
                true,
                requestSource,
                duration: stopwatch.Elapsed,
                cancellationToken: cancellationToken);

            _tracingService.AddAnnotation("success", true);
            _tracingService.AddAnnotation("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation(
                "Operation completed successfully: {Operation} | Duration: {Duration}ms | TraceId: {TraceId}",
                operationName, stopwatch.ElapsedMilliseconds, traceId);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await _metricsService.RecordProcessingMetricAsync(
                operationName,
                false,
                requestSource,
                duration: stopwatch.Elapsed,
                cancellationToken: cancellationToken);

            _tracingService.AddAnnotation("success", false);
            _tracingService.AddAnnotation("error", ex.Message);
            _tracingService.RecordException(ex);

            _logger.LogError(ex,
                "Operation failed: {Operation} | Duration: {Duration}ms | TraceId: {TraceId} | Error: {Error}",
                operationName, stopwatch.ElapsedMilliseconds, traceId, ex.Message);

            throw;
        }
    }

    public void LogWithTraceId(LogLevel logLevel, string message, params object[] args)
    {
        var traceId = _tracingService.GetCurrentTraceId();
        var enrichedMessage = $"{message} | TraceId: {{TraceId}}";
        var enrichedArgs = args.Concat(new object[] { traceId ?? "unknown" }).ToArray();

        _logger.Log(logLevel, enrichedMessage, enrichedArgs);
    }

    public string? GetCurrentTraceId()
    {
        return _tracingService.GetCurrentTraceId();
    }

    public async Task RecordMetricAsync(string metricName, double value, Dictionary<string, string>? dimensions = null)
    {
        await _metricsService.RecordCustomMetricAsync(metricName, value, "Count", dimensions);
    }
}