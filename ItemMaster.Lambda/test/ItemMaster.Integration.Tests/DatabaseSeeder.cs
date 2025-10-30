using System.Text.Json;
using ItemMaster.Infrastructure.Ef;
using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;

namespace ItemMaster.Integration.Tests;

public class DatabaseSeeder
{
    private readonly MySqlDbContext _dbContext;

    public DatabaseSeeder(MySqlDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedTestDataAsync()
    {
        var existingLogs = await _dbContext.ItemMasterSourceLogs.CountAsync();
        if (existingLogs > 0)
        {
            Console.WriteLine("Test data already exists, skipping seed");
            return;
        }

        var testItems = CreateTestItems();

        foreach (var item in testItems)
        {
            var log = new ItemMasterSourceLog
            {
                Sku = item.Sku,
                ValidationStatus = "valid",
                IsSentToSqs = false,
                CreatedAt = DateTime.UtcNow,
                SourceModel = JsonSerializer.Serialize(item)
            };

            _dbContext.ItemMasterSourceLogs.Add(log);
        }

        await _dbContext.SaveChangesAsync();
        Console.WriteLine($"Seeded {testItems.Count} test items into database");
    }

    public async Task CleanupTestDataAsync()
    {
        var logs = await _dbContext.ItemMasterSourceLogs.ToListAsync();
        _dbContext.ItemMasterSourceLogs.RemoveRange(logs);
        await _dbContext.SaveChangesAsync();
        Console.WriteLine("Cleaned up test data from database");
    }

    private static List<ItemMasterSourceLog> CreateTestItems()
    {
        return new List<ItemMasterSourceLog>
        {
            new()
            {
                Sku = "TEST-001",
                ValidationStatus = "valid",
                IsSentToSqs = false,
                CreatedAt = DateTime.UtcNow,
                SourceModel = "{\"Sku\":\"TEST-001\",\"ProductTitle\":\"Test Item 1\"}"
            },
            new()
            {
                Sku = "TEST-002",
                ValidationStatus = "valid",
                IsSentToSqs = false,
                CreatedAt = DateTime.UtcNow,
                SourceModel = "{\"Sku\":\"TEST-002\",\"ProductTitle\":\"Test Item 2\"}"
            }
        };
    }
}