using System.Diagnostics.CodeAnalysis;
using ItemMaster.Infrastructure.Ef;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ItemMaster.Integration.Tests;

[ExcludeFromCodeCoverage]
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestSetup>
{
}

[ExcludeFromCodeCoverage]
public class IntegrationTestSetup : IDisposable
{
    private readonly string _connectionString;
    private MySqlDbContext? _dbContext;

    public IntegrationTestSetup()
    {
        _connectionString = GetTestConnectionString();
        ApplyMigrations();
        SeedTestData();
    }

    public void Dispose()
    {
        CleanupTestData();
        _dbContext?.Dispose();
    }

    private static string GetTestConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
        var database = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "item_master";
        var user = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "im_user";
        var password = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "im_pass";

        return $"Server={host};Database={database};User={user};Password={password};CharSet=utf8mb4;";
    }

    private void ApplyMigrations()
    {
        try
        {
            MigrationHelper.ApplyMigrations(_connectionString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Migration failed: {ex.Message}");
            throw;
        }
    }

    private void SeedTestData()
    {
        var optionsBuilder = new DbContextOptionsBuilder<MySqlDbContext>();
        optionsBuilder.UseMySql(_connectionString, new MySqlServerVersion(new Version(8, 0, 35)));

        _dbContext = new MySqlDbContext(optionsBuilder.Options);

        var seeder = new DatabaseSeeder(_dbContext);
        seeder.SeedTestDataAsync().GetAwaiter().GetResult();
    }

    private void CleanupTestData()
    {
        if (_dbContext == null) return;

        try
        {
            var seeder = new DatabaseSeeder(_dbContext);
            seeder.CleanupTestDataAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cleanup failed: {ex.Message}");
        }
    }
}