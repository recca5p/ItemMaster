using ItemMaster.Infrastructure.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Lambda.Services;

public interface IDatabaseMigrationService
{
    void ApplyMigrations();
}

public class DatabaseMigrationService : IDatabaseMigrationService
{
    private readonly MySqlDbContext _dbContext;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private static bool _migrationsApplied;

    public DatabaseMigrationService(MySqlDbContext dbContext, ILogger<DatabaseMigrationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public void ApplyMigrations()
    {
        if (_migrationsApplied)
        {
            _logger.LogInformation("Database migrations already applied, skipping");
            return;
        }

        try
        {
            _logger.LogInformation("Applying database migrations");
            _dbContext.Database.Migrate();
            _migrationsApplied = true;
            _logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }
    }
}
