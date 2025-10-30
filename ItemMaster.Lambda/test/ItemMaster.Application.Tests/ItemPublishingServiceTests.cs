using FluentAssertions;
using ItemMaster.Application.Services;
using ItemMaster.Contracts;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Application.Tests;

public class ItemPublishingServiceTests
{
    [Fact]
    public async Task PublishItemsAsync_OnSuccess_ShouldMarkLogsSent()
    {
        // Arrange
        var publisher = new Mock<IItemPublisher>();
        publisher
            .Setup(p => p.PublishUnifiedItemsAsync(It.IsAny<IEnumerable<UnifiedItemMaster>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var obs = new Mock<IObservabilityService>();
        obs.Setup(o => o.ExecuteWithObservabilityAsync<Result>(
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<Func<Task<Result>>>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string name, RequestSource src, Func<Task<Result>> func, Dictionary<string, object>? meta, CancellationToken ct) => func());

        var repo = new Mock<IItemMasterLogRepository>();
        repo.Setup(r => r.MarkSentToSqsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var logger = new Mock<ILogger<ItemPublishingService>>();
        var service = new ItemPublishingService(publisher.Object, obs.Object, logger.Object, repo.Object);

        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "SKU-1", Name = "X" }
        };

        // Act
        await service.PublishItemsAsync(items, RequestSource.ApiGateway, "trace-1", CancellationToken.None);

        // Assert
        repo.Verify(r => r.MarkSentToSqsAsync(It.Is<IEnumerable<string>>(s => s.Contains("SKU-1")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishItemsAsync_OnFailure_ShouldNotMarkLogsSent()
    {
        // Arrange
        var publisher = new Mock<IItemPublisher>();
        publisher
            .Setup(p => p.PublishUnifiedItemsAsync(It.IsAny<IEnumerable<UnifiedItemMaster>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("err"));

        var obs = new Mock<IObservabilityService>();
        obs.Setup(o => o.ExecuteWithObservabilityAsync<Result>(
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<Func<Task<Result>>>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string name, RequestSource src, Func<Task<Result>> func, Dictionary<string, object>? meta, CancellationToken ct) => func());

        var repo = new Mock<IItemMasterLogRepository>();
        var logger = new Mock<ILogger<ItemPublishingService>>();
        var service = new ItemPublishingService(publisher.Object, obs.Object, logger.Object, repo.Object);

        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "SKU-1", Name = "X" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.PublishItemsAsync(items, RequestSource.ApiGateway, "trace-1", CancellationToken.None));

        repo.Verify(r => r.MarkSentToSqsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}


