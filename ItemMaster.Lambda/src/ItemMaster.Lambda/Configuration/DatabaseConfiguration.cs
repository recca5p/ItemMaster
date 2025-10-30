namespace ItemMaster.Lambda.Configuration;

public class DatabaseConfiguration
{
    public const string SECTION_NAME = "Database";

    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
    public bool EnableSensitiveDataLogging { get; set; } = false;
}