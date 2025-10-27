namespace ItemMaster.Lambda.Configuration;

public class LambdaStartupOptions
{
    public const string SectionName = "LambdaStartup";

    public string Environment { get; set; } = string.Empty;
    public string ConfigBase { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool TestMode { get; set; }
    public bool ApplyMigrations { get; set; }
    public string LogLevel { get; set; } = ConfigurationConstants.DEFAULT_LOG_LEVEL;
}