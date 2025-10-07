namespace ItemMaster.Shared;

public interface IConnectionStringProvider
{
    string? GetMySqlConnectionString();
}

