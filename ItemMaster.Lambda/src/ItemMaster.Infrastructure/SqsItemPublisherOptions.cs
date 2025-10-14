namespace ItemMaster.Infrastructure;

public class SqsItemPublisherOptions
{
    public string QueueUrl { get; set; } = string.Empty;
    public int MaxRetries { get; set; } = 2;
    public int BaseDelayMs { get; set; } = 1000;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int BatchSize { get; set; } = 100;

    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerDurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);
    public int CircuitBreakerSamplingDuration { get; set; } = 60;
    public int CircuitBreakerMinimumThroughput { get; set; } = 3;
}