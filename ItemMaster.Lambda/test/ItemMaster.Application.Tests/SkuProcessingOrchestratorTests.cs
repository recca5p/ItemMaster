using FluentAssertions;
using ItemMaster.Application.Services;
using ItemMaster.Contracts;
using ItemMaster.Domain;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Application.Tests;

public class SkuProcessingOrchestratorTests
{
    private readonly Mock<IItemFetchingService> _mockFetchingService;
    private readonly Mock<ILogger<SkuProcessingOrchestrator>> _mockLogger;
    private readonly Mock<IItemMappingService> _mockMappingService;
    private readonly Mock<IObservabilityService> _mockObservabilityService;
    private readonly Mock<IItemPublishingService> _mockPublishingService;
    private readonly Mock<IProcessingResponseBuilder> _mockResponseBuilder;
    private readonly Mock<ISkuAnalysisService> _mockSkuAnalysisService;
    private readonly SkuProcessingOrchestrator _orchestrator;

    public SkuProcessingOrchestratorTests()
    {
        _mockFetchingService = new Mock<IItemFetchingService>();
        _mockMappingService = new Mock<IItemMappingService>();
        _mockPublishingService = new Mock<IItemPublishingService>();
        _mockSkuAnalysisService = new Mock<ISkuAnalysisService>();
        _mockResponseBuilder = new Mock<IProcessingResponseBuilder>();
        _mockObservabilityService = new Mock<IObservabilityService>();
        _mockLogger = new Mock<ILogger<SkuProcessingOrchestrator>>();

        _orchestrator = new SkuProcessingOrchestrator(
            _mockFetchingService.Object,
            _mockMappingService.Object,
            _mockPublishingService.Object,
            _mockSkuAnalysisService.Object,
            _mockResponseBuilder.Object,
            _mockObservabilityService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessSkusAsync_WithHealthCheckRequest_ShouldReturnSuccessWithoutProcessing()
    {
        // Arrange
        var request = new ProcessSkusRequest();
        _mockObservabilityService.Setup(x => x.ExecuteWithObservabilityAsync(
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<Func<Task<Result<ProcessSkusResponse>>>>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .Returns((string op, RequestSource src, Func<Task<Result<ProcessSkusResponse>>> fn,
                Dictionary<string, object> meta, CancellationToken ct) =>
            {
                return Task.FromResult(Result<ProcessSkusResponse>.Success(new ProcessSkusResponse
                {
                    Success = true,
                    ItemsProcessed = 0,
                    ItemsPublished = 0,
                    Failed = 0
                }));
            });

        // Act
        var result = await _orchestrator.ProcessSkusAsync(request, RequestSource.CicdHealthCheck, "trace-health",
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ItemsProcessed.Should().Be(0);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessSkusAsync_WithValidSkus_ShouldExecuteFullWorkflow()
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "TEST-001" } };
        var items = CreateTestItems(1);
        var mappingResult = new ItemMappingResult { UnifiedItems = new List<UnifiedItemMaster>() };
        var expectedResponse = new ProcessSkusResponse { Success = true };

        _mockObservabilityService.Setup(x => x.ExecuteWithObservabilityAsync(
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<Func<Task<Result<ProcessSkusResponse>>>>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string op, RequestSource src, Func<Task<Result<ProcessSkusResponse>>> fn,
                Dictionary<string, object> meta, CancellationToken ct) => await fn());

        _mockFetchingService.Setup(x => x.FetchItemsBySkusAsync(It.IsAny<List<string>>(), It.IsAny<RequestSource>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        _mockMappingService.Setup(x =>
                x.MapItemsAsync(It.IsAny<List<Item>>(), It.IsAny<RequestSource>(), It.IsAny<string>()))
            .ReturnsAsync(mappingResult);
        _mockSkuAnalysisService.Setup(x => x.AnalyzeNotFoundSkus(It.IsAny<List<string>>(), It.IsAny<List<Item>>()))
            .Returns(new List<string>());
        _mockResponseBuilder.Setup(x =>
                x.CreateSuccessResponse(It.IsAny<List<Item>>(), It.IsAny<List<string>>(),
                    It.IsAny<ItemMappingResult>()))
            .Returns(expectedResponse);

        // Act
        var result = await _orchestrator.ProcessSkusAsync(request, RequestSource.ApiGateway, "trace-workflow",
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockFetchingService.Verify(
            x => x.FetchItemsBySkusAsync(It.IsAny<List<string>>(), It.IsAny<RequestSource>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        _mockMappingService.Verify(
            x => x.MapItemsAsync(It.IsAny<List<Item>>(), It.IsAny<RequestSource>(), It.IsAny<string>()), Times.Once);
        _mockPublishingService.Verify(
            x => x.PublishItemsAsync(It.IsAny<List<UnifiedItemMaster>>(), It.IsAny<RequestSource>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessSkusAsync_WithEmptySkus_ShouldFetchLatestItems()
    {
        // Arrange
        var request = new ProcessSkusRequest();
        var items = CreateTestItems(10);
        var mappingResult = new ItemMappingResult { UnifiedItems = new List<UnifiedItemMaster>() };
        var expectedResponse = new ProcessSkusResponse { Success = true };

        _mockObservabilityService.Setup(x => x.ExecuteWithObservabilityAsync(
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<Func<Task<Result<ProcessSkusResponse>>>>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string op, RequestSource src, Func<Task<Result<ProcessSkusResponse>>> fn,
                Dictionary<string, object> meta, CancellationToken ct) => await fn());

        _mockFetchingService.Setup(x => x.FetchLatestItemsAsync(It.IsAny<int>(), It.IsAny<RequestSource>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        _mockMappingService.Setup(x =>
                x.MapItemsAsync(It.IsAny<List<Item>>(), It.IsAny<RequestSource>(), It.IsAny<string>()))
            .ReturnsAsync(mappingResult);
        _mockSkuAnalysisService.Setup(x => x.AnalyzeNotFoundSkus(It.IsAny<List<string>>(), It.IsAny<List<Item>>()))
            .Returns(new List<string>());
        _mockResponseBuilder.Setup(x =>
                x.CreateSuccessResponse(It.IsAny<List<Item>>(), It.IsAny<List<string>>(),
                    It.IsAny<ItemMappingResult>()))
            .Returns(expectedResponse);

        // Act
        var result =
            await _orchestrator.ProcessSkusAsync(request, RequestSource.Sqs, "trace-latest", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockFetchingService.Verify(
            x => x.FetchLatestItemsAsync(It.IsAny<int>(), It.IsAny<RequestSource>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    private static List<Item> CreateTestItems(int count)
    {
        return Enumerable.Range(1, count).Select(i => new Item
        {
            Sku = $"TEST-{i:D3}",
            ProductTitle = $"Product {i}",
            Barcode = "1234567890123",
            Hts = "1234567890",
            CountryOfOrigin = "US",
            Price = 29.99f,
            LandedCost = 18.00f,
            Size = "M",
            Color = "Blue",
            FabricContent = "Cotton",
            FabricComposition = "100% Cotton"
        }).ToList();
    }
}