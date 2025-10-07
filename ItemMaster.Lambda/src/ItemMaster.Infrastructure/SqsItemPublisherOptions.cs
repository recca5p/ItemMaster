namespace ItemMaster.Infrastructure;

public class SqsItemPublisherOptions
{
    public string QueueUrl { get; init; } = string.Empty;
    public int MaxRetries { get; init; } = 3;
    public int BaseDelayMs { get; init; } = 1000;
    public double BackoffMultiplier { get; init; } = 2.0;
    public int BatchSize { get; init; } = 100;
}

