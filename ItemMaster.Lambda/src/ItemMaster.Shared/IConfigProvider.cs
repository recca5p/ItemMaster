namespace ItemMaster.Shared;

public interface IConfigProvider
{
    string? GetConfigValue(string key);
}