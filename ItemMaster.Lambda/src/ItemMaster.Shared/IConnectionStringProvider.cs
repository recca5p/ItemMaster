namespace ItemMaster.Shared;

public interface IConnectionStringProvider
{
    Task<string> GetMySqlConnectionStringAsync();
}