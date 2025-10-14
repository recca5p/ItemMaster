namespace ItemMaster.Infrastructure.Secrets;

public interface ISnowflakeConnectionProvider
{
    Task<string> GetConnectionStringAsync();
}