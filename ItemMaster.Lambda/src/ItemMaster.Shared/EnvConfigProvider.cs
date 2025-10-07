namespace ItemMaster.Shared;

public sealed class EnvConfigProvider : IConfigProvider
{
    public string? Get(string key, string? defaultValue = null)
        => Environment.GetEnvironmentVariable(key) ?? defaultValue;

    public T Get<T>(string key, T defaultValue = default!)
    {
        var raw = Get(key);
        if (raw is null) return defaultValue;
        try
        {
            return (T)Convert.ChangeType(raw, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}

