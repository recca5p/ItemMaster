namespace ItemMaster.Lambda.Configuration;

public class ProcessingConfiguration
{
    public const string SECTION_NAME = "Processing";

    public int DefaultLatestItemsLimit { get; set; } = 100;
    public int MaxBatchSize { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 300;
}