using ItemMaster.Infrastructure.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItemMaster.Integration.Tests;

public class MigrationHelper
{
    public static void ApplyMigrations(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<MySqlDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35))));

        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MySqlDbContext>();

        dbContext.Database.Migrate();
        Console.WriteLine("Database migrations applied successfully");
    }
}