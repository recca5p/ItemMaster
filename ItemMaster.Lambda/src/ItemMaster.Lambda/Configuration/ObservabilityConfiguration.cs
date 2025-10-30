namespace ItemMaster.Lambda.Configuration;

public class ObservabilityConfiguration
{
    public const string SECTION_NAME = "Observability";

    public bool EnableMetrics { get; set; } = true;
    public bool EnableTracing { get; set; } = true;
    public int MetricsBatchSize { get; set; } = 100;
    public int TracingSampleRate { get; set; } = 10;
}