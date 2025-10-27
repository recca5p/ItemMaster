using Microsoft.Extensions.DependencyInjection;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Lambda.Services;

public interface IConnectionStringService
{
    string GetConnectionString(ServiceProvider serviceProvider);
}

public class ConnectionStringService : IConnectionStringService
{
    private readonly ILogger<ConnectionStringService> _logger;

    public ConnectionStringService(ILogger<ConnectionStringService> logger)
    {
        _logger = logger;
    }

    public string GetConnectionString(ServiceProvider serviceProvider)
    {
        try
        {
            using var tempScope = serviceProvider.CreateScope();
            var connectionProvider = tempScope.ServiceProvider.GetRequiredService<IConnectionStringProvider>();
            return connectionProvider.GetMySqlConnectionStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve database connection string");
            throw new InvalidOperationException("Connection string resolution failed", ex);
        }
    }
}
