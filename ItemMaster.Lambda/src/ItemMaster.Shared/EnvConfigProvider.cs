namespace ItemMaster.Shared;

public class EnvConfigProvider : IConfigProvider
{
    public string? GetConfigValue(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }
}