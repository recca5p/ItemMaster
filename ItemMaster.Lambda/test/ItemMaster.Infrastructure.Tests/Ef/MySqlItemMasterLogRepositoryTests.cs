using FluentAssertions;
using ItemMaster.Infrastructure.Ef;
using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Ef;

public class MySqlItemMasterLogRepositoryTests
{
    private readonly MySqlDbContext _dbContext;
    private readonly Mock<IClock> _mockClock;
    private readonly Mock<ILogger<MySqlItemMasterLogRepository>> _mockLogger;

    public MySqlItemMasterLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MySqlDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MySqlDbContext(options);
        _mockClock = new Mock<IClock>();
        _mockLogger = new Mock<ILogger<MySqlItemMasterLogRepository>>();

        var now = DateTime.UtcNow;
        _mockClock.Setup(x => x.UtcNow).Returns(now);
    }

    [Fact]
    public async Task LogItemSourceAsync_WithValidLog_ShouldReturnSuccess()
    {
        var repository = new MySqlItemMasterLogRepository(_dbContext, _mockClock.Object, _mockLogger.Object);
        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        var result = await repository.LogItemSourceAsync(log);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LogItemSourceAsync_ShouldSetCreatedAtTimestamp()
    {
        var repository = new MySqlItemMasterLogRepository(_dbContext, _mockClock.Object, _mockLogger.Object);
        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        await repository.LogItemSourceAsync(log);

        log.CreatedAt.Should().BeCloseTo(_mockClock.Object.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task LogItemSourceAsync_ShouldSaveToDatabase()
    {
        var repository = new MySqlItemMasterLogRepository(_dbContext, _mockClock.Object, _mockLogger.Object);
        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        await repository.LogItemSourceAsync(log);

        var savedLog = await _dbContext.ItemMasterSourceLogs.FirstOrDefaultAsync(x => x.Sku == "TEST-001");
        savedLog.Should().NotBeNull();
        savedLog!.Sku.Should().Be("TEST-001");
        savedLog.ValidationStatus.Should().Be("Valid");
        savedLog.IsSentToSqs.Should().BeTrue();
    }

    [Fact]
    public async Task LogItemSourceAsync_WithException_ShouldReturnFailure()
    {
        var disposedContext = new MySqlDbContext(new DbContextOptionsBuilder<MySqlDbContext>().Options);
        var repository = new MySqlItemMasterLogRepository(disposedContext, _mockClock.Object, _mockLogger.Object);
        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        var result = await repository.LogItemSourceAsync(log);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MySQL item source logging failed");
    }

    [Fact]
    public async Task LogItemSourceAsync_WithMultipleLogs_ShouldSaveAll()
    {
        var repository = new MySqlItemMasterLogRepository(_dbContext, _mockClock.Object, _mockLogger.Object);
        var logs = new[]
        {
            new ItemMasterSourceLog { Sku = "TEST-001", ValidationStatus = "Valid", IsSentToSqs = true },
            new ItemMasterSourceLog { Sku = "TEST-002", ValidationStatus = "Invalid", IsSentToSqs = false },
            new ItemMasterSourceLog { Sku = "TEST-003", ValidationStatus = "Valid", IsSentToSqs = true }
        };

        foreach (var log in logs) await repository.LogItemSourceAsync(log);

        var count = await _dbContext.ItemMasterSourceLogs.CountAsync();
        count.Should().Be(3);
    }
}

public class EfItemMasterLogRepositoryTests
{
    private readonly MySqlDbContext _dbContext;
    private readonly Mock<IClock> _mockClock;
    private readonly Mock<ILogger<EfItemMasterLogRepository>> _mockLogger;

    public EfItemMasterLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MySqlDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MySqlDbContext(options);
        _mockClock = new Mock<IClock>();
        _mockLogger = new Mock<ILogger<EfItemMasterLogRepository>>();

        var now = DateTime.UtcNow;
        _mockClock.Setup(x => x.UtcNow).Returns(now);
    }

    [Fact]
    public async Task LogItemSourceAsync_WithValidLog_ShouldReturnSuccess()
    {
        var repository = new EfItemMasterLogRepository(_dbContext, _mockClock.Object, _mockLogger.Object);
        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        var result = await repository.LogItemSourceAsync(log);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LogItemSourceAsync_ShouldSetCreatedAtTimestamp()
    {
        var repository = new EfItemMasterLogRepository(_dbContext, _mockClock.Object, _mockLogger.Object);
        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        await repository.LogItemSourceAsync(log);

        log.CreatedAt.Should().BeCloseTo(_mockClock.Object.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task LogItemSourceAsync_WithException_ShouldReturnFailure()
    {
        var disposedContext = new MySqlDbContext(new DbContextOptionsBuilder<MySqlDbContext>().Options);
        var repository = new EfItemMasterLogRepository(disposedContext, _mockClock.Object, _mockLogger.Object);
        var log = new ItemMasterSourceLog
        {
            Sku = "TEST-001",
            ValidationStatus = "Valid",
            IsSentToSqs = true
        };

        var result = await repository.LogItemSourceAsync(log);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MySQL item source logging failed");
    }
}