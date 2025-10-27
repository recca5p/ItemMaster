namespace ItemMaster.Lambda.Configuration;

public class CacheConfiguration
{
    public const string SECTION_NAME = "Cache";
    
    public int SecretCacheDurationMinutes { get; set; } = 15;
    public int MaxCacheSize { get; set; } = 1000;
    public bool EnableDistributedCache { get; set; } = false;
}
