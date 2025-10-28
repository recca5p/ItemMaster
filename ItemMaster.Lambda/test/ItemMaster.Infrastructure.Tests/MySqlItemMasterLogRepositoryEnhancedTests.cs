using FluentAssertions;
using ItemMaster.Infrastructure;
using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class MySqlItemMasterLogRepositoryEnhancedTests
{
    public static IEnumerable<object[]> GetLogItemSourceTestData()
    {
        yield return new object[]
        {
            new ItemMasterSourceLog
            {
                Sku = "TEST-001",
                ValidationStatus = "Valid",
                IsSentToSqs = true
            },
            true,
            "Valid log entry should succeed"
        };

        yield return new object[]
        {
            new ItemMasterSourceLog
            {
                Sku = "TEST-002",
                ValidationStatus = "Invalid",
                IsSentToSqs = false
            },
            true,
            "Invalid validation status entry should still succeed"
        };

        yield return new object[]
        {
            new ItemMasterSourceLog
            {
                Sku = "TEST-003",
                ValidationStatus = "Valid",
                IsSentToSqs = false
            },
            true,
            "Entry not sent to SQS should still succeed"
        };
    }

    [Theory]
    [MemberData(nameof(GetLogItemSourceTestData))]
    public async Task LogItemSourceAsync_WithDifferentLogEntries_ShouldSaveCorrectly(
        ItemMasterSourceLog log,
        bool shouldSucceed,
        string scenario)
    {
        var options = new DbContextOptionsBuilder<ItemMaster.Infrastructure.Ef.MySqlDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new ItemMaster.Infrastructure.Ef.MySqlDbContext(options);
        var mockClock = new Mock<IClock>();
        var mockLogger = new Mock<ILogger<MySqlItemMasterLogRepository>>();

        var now = DateTime.UtcNow;
        mockClock.Setup(x => x.UtcNow).Returns(now);

        var repository = new MySqlItemMasterLogRepository(dbContext, mockClock.Object, mockLogger.Object);

        var result = await repository.LogItemSourceAsync(log);

        if (shouldSucceed)
        {
            result.IsSuccess.Should().BeTrue(scenario);
            log.CreatedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task LogItemSourceAsync_WithCancellation_ShouldRespectCancellation()
    {
        var options = new DbContextOptionsBuilder<ItemMaster.Infrastructure.Ef.MySqlDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new ItemMaster.Infrastructure.Ef.MySqlDbContext(options);
        var mockClock = new Mock<IClock>();
        var mockLogger = new Mock<ILogger<MySqlItemMasterLogRepository>>();

        var now = DateTime.UtcNow;
        mockClock.Setup(x => x.UtcNow).Returns(now);

        var repository = new MySqlItemMasterLogRepository(dbContext, mockClock.Object, mockLogger.Object);

        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await repository.LogItemSourceAsync(log, cts.Token);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MySQL item source logging failed");
    }

    [Fact]
    public async Task LogItemSourceAsync_WithMultipleLogs_ShouldLogAllSuccessfully()
    {
        var options = new DbContextOptionsBuilder<ItemMaster.Infrastructure.Ef.MySqlDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new ItemMaster.Infrastructure.Ef.MySqlDbContext(options);
        var mockClock = new Mock<IClock>();
        var mockLogger = new Mock<ILogger<MySqlItemMasterLogRepository>>();

        var now = DateTime.UtcNow;
        mockClock.Setup(x => x.UtcNow).Returns(now);

        var repository = new MySqlItemMasterLogRepository(dbContext, mockClock.Object, mockLogger.Object);

        var logs = new[]
        {
            new ItemMasterSourceLog { Sku = "TEST-001", ValidationStatus = "Valid", IsSentToSqs = true },
            new ItemMasterSourceLog { Sku = "TEST-002", ValidationStatus = "Invalid", IsSentToSqs = false },
            new ItemMasterSourceLog { Sku = "TEST-003", ValidationStatus = "Valid", IsSentToSqs = true },
            new ItemMasterSourceLog { Sku = "TEST-004", ValidationStatus = "Pending", IsSentToSqs = false }
        };

        foreach (var log in logs)
        {
            var result = await repository.LogItemSourceAsync(log);
            result.IsSuccess.Should().BeTrue();
        }

        var savedLogs = await dbContext.ItemMasterSourceLogs.ToListAsync();
        savedLogs.Should().HaveCount(4);
        savedLogs.Select(x => x.Sku).Should().Contain("TEST-001", "TEST-002", "TEST-003", "TEST-004");
    }

    [Fact]
    public async Task LogItemSourceAsync_WithDatabaseException_ShouldLogError()
    {
        var mockClock = new Mock<IClock>();
        var mockLogger = new Mock<ILogger<MySqlItemMasterLogRepository>>();

        var now = DateTime.UtcNow;
        mockClock.Setup(x => x.UtcNow).Returns(now);

        var options = new DbContextOptionsBuilder<ItemMaster.Infrastructure.Ef.MySqlDbContext>()
            .Options;

        var dbContext = new ItemMaster.Infrastructure.Ef.MySqlDbContext(options);

        var repository = new MySqlItemMasterLogRepository(dbContext, mockClock.Object, mockLogger.Object);

        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        var result = await repository.LogItemSourceAsync(log);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MySQL item source logging failed");
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogItemSourceAsync_ShouldSetCreatedAtTimestamp()
    {
        var options = new DbContextOptionsBuilder<ItemMaster.Infrastructure.Ef.MySqlDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new ItemMaster.Infrastructure.Ef.MySqlDbContext(options);
        var mockClock = new Mock<IClock>();
        var mockLogger = new Mock<ILogger<MySqlItemMasterLogRepository>>();

        var specificTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        mockClock.Setup(x => x.UtcNow).Returns(specificTime);

        var repository = new MySqlItemMasterLogRepository(dbContext, mockClock.Object, mockLogger.Object);

        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        await repository.LogItemSourceAsync(log);

        log.CreatedAt.Should().Be(specificTime);
    }
}

