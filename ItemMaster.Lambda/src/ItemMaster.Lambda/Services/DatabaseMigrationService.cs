using System.Diagnostics.CodeAnalysis;
using ItemMaster.Infrastructure.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Lambda.Services;

public interface IDatabaseMigrationService
{
    void ApplyMigrations();
}

[ExcludeFromCodeCoverage]
public class DatabaseMigrationService : IDatabaseMigrationService
{
    private static bool _migrationsApplied;
    private readonly MySqlDbContext _dbContext;
    private readonly ILogger<DatabaseMigrationService> _logger;

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