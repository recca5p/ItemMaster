using FluentAssertions;
using ItemMaster.Infrastructure.Ef;
using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Ef;

public class MarkSentToSqsTests
{
    private MySqlDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MySqlDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MySqlDbContext(options);
    }

    [Fact]
    public async Task MarkSentToSqsAsync_ShouldSetTrue_ForLatestRows()
    {
        // Arrange
        using var ctx = CreateContext();
        var clock = new Mock<IClock>();
        var logger = new Mock<ILogger<EfItemMasterLogRepository>>();
        var repo = new EfItemMasterLogRepository(ctx, clock.Object, logger.Object);

        var baseTime = DateTime.UtcNow;
        clock.Setup(x => x.UtcNow).Returns(baseTime);

        var sku = "SKU-1";
        ctx.ItemMasterSourceLogs.AddRange(
            new ItemMasterSourceLog { Sku = sku, ValidationStatus = "valid", IsSentToSqs = false, CreatedAt = baseTime.AddMinutes(-10) },
            new ItemMasterSourceLog { Sku = sku, ValidationStatus = "valid", IsSentToSqs = false, CreatedAt = baseTime.AddMinutes(-1) }
        );
        await ctx.SaveChangesAsync();

        // Act
        var result = await repo.MarkSentToSqsAsync(new[] { sku });

        // Assert
        result.IsSuccess.Should().BeTrue();
        var rows = await ctx.ItemMasterSourceLogs.Where(x => x.Sku == sku).OrderBy(x => x.CreatedAt).ToListAsync();
        rows[0].IsSentToSqs.Should().BeFalse();
        rows[1].IsSentToSqs.Should().BeTrue();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public async Task MarkSentToSqsAsync_WithEmptySkus_ShouldBeNoop(string bad)
    {
        // Arrange
        using var ctx = CreateContext();
        var clock = new Mock<IClock>();
        var logger = new Mock<ILogger<EfItemMasterLogRepository>>();
        var repo = new EfItemMasterLogRepository(ctx, clock.Object, logger.Object);

        // Act
        var result = await repo.MarkSentToSqsAsync(new[] { bad });

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await ctx.ItemMasterSourceLogs.CountAsync()).Should().Be(0);
    }
}


