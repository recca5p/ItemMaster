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

public class ItemMappingServiceTests
{
    private readonly Mock<ILogger<ItemMappingService>> _mockLogger;
    private readonly Mock<IItemMasterLogRepository> _mockLogRepository;
    private readonly Mock<IUnifiedItemMapper> _mockMapper;
    private readonly Mock<IObservabilityService> _mockObservabilityService;
    private readonly ItemMappingService _service;

    public ItemMappingServiceTests()
    {
        _mockMapper = new Mock<IUnifiedItemMapper>();
        _mockLogRepository = new Mock<IItemMasterLogRepository>();
        _mockObservabilityService = new Mock<IObservabilityService>();
        _mockLogger = new Mock<ILogger<ItemMappingService>>();

        _service = new ItemMappingService(
            _mockMapper.Object,
            _mockLogRepository.Object,
            _mockObservabilityService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task MapItemsAsync_WithValidItems_ShouldMapSuccessfully()
    {
        // Arrange
        var items = CreateValidItems(2);
        var mappingResult = MappingResult.Success(CreateValidUnifiedItem(), "TEST-001");
        _mockMapper.Setup(x => x.MapToUnifiedModel(It.IsAny<Item>())).Returns(mappingResult);

        // Act
        var result = await _service.MapItemsAsync(items, RequestSource.ApiGateway, "trace-123");

        // Assert
        result.Should().NotBeNull();
        result.UnifiedItems.Should().HaveCount(2);
        result.SuccessfulSkus.Should().HaveCount(2);
        result.SkippedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task MapItemsAsync_WithFailedMappings_ShouldTrackSkippedItems()
    {
        // Arrange
        var items = CreateValidItems(2);
        var failureResult = MappingResult.Failure("TEST-001", new List<string> { "Missing required Field" });
        _mockMapper.Setup(x => x.MapToUnifiedModel(It.IsAny<Item>())).Returns(failureResult);

        // Act
        var result = await _service.MapItemsAsync(items, RequestSource.Sqs, "trace-456");

        // Assert
        result.Should().NotBeNull();
        result.UnifiedItems.Should().BeEmpty();
        result.SuccessfulSkus.Should().BeEmpty();
        result.SkippedItems.Should().HaveCount(2);
        result.SkippedItems[0].Reason.Should().Be("Validation failed");
    }

    [Fact]
    public async Task MapItemsAsync_WithMixedResults_ShouldTrackBothSuccessAndFailures()
    {
        // Arrange
        var items = CreateValidItems(3);

        var successResult = MappingResult.Success(CreateValidUnifiedItem(), "TEST-001");
        var failureResult = MappingResult.Failure("TEST-002", new List<string> { "Validation error" });

        _mockMapper.SetupSequence(x => x.MapToUnifiedModel(It.IsAny<Item>()))
            .Returns(successResult)
            .Returns(failureResult)
            .Returns(successResult);

        // Act
        var result = await _service.MapItemsAsync(items, RequestSource.ApiGateway, "trace-789");

        // Assert
        result.UnifiedItems.Should().HaveCount(2);
        result.SuccessfulSkus.Should().HaveCount(2);
        result.SkippedItems.Should().HaveCount(1);
        result.SkippedItems[0].ValidationFailure.Should().Be("Validation error");
    }

    [Fact]
    public async Task MapItemsAsync_WithSkippedProperties_ShouldLogWarning()
    {
        // Arrange
        var items = CreateValidItems(1);
        var skippedProps = new List<string> { "Attribute.gender", "Attribute.velocity_code" };
        var mappingResult = MappingResult.Success(CreateValidUnifiedItem(), "TEST-001", skippedProps);
        _mockMapper.Setup(x => x.MapToUnifiedModel(It.IsAny<Item>())).Returns(mappingResult);

        // Act
        var result = await _service.MapItemsAsync(items, RequestSource.ApiGateway, "trace-warn");

        // Assert
        result.Should().NotBeNull();
        result.PublishedItems.Should().HaveCount(1);
        result.PublishedItems[0].SkippedProperties.Should().HaveCount(2);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task MapItemsAsync_WithEmptyItemList_ShouldReturnEmptyResult()
    {
        // Arrange
        var items = new List<Item>();

        // Act
        var result = await _service.MapItemsAsync(items, RequestSource.ApiGateway, "trace-empty");

        // Assert
        result.Should().NotBeNull();
        result.UnifiedItems.Should().BeEmpty();
        result.SuccessfulSkus.Should().BeEmpty();
        result.SkippedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task MapItemsAsync_ShouldLogToRepository()
    {
        // Arrange
        var items = CreateValidItems(1);
        var mappingResult = MappingResult.Success(CreateValidUnifiedItem(), "TEST-001");
        _mockMapper.Setup(x => x.MapToUnifiedModel(It.IsAny<Item>())).Returns(mappingResult);

        // Act
        await _service.MapItemsAsync(items, RequestSource.Sqs, "trace-repo");

        // Assert
        _mockLogRepository.Verify(
            x => x.LogItemSourceAsync(It.IsAny<ItemMasterSourceLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MapItemsAsync_WithSkippedItems_ShouldRecordMetrics()
    {
        // Arrange
        var items = CreateValidItems(2);
        var failureResult = MappingResult.Failure("TEST-001", new List<string> { "Error" });
        _mockMapper.Setup(x => x.MapToUnifiedModel(It.IsAny<Item>())).Returns(failureResult);

        // Act
        await _service.MapItemsAsync(items, RequestSource.ApiGateway, "trace-metrics");

        // Assert
        _mockObservabilityService.Verify(
            x => x.RecordMetricAsync("ItemsSkippedValidation", 2.0,
                It.Is<Dictionary<string, string>>(d => d["requestSource"] == "ApiGateway")),
            Times.Once);
    }

    [Fact]
    public async Task MapItemsAsync_WithLoggingFailure_ShouldContinueProcessing()
    {
        // Arrange
        var items = CreateValidItems(1);
        var mappingResult = MappingResult.Success(CreateValidUnifiedItem(), "TEST-001");
        _mockMapper.Setup(x => x.MapToUnifiedModel(It.IsAny<Item>())).Returns(mappingResult);
        _mockLogRepository.Setup(x =>
                x.LogItemSourceAsync(It.IsAny<ItemMasterSourceLog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _service.MapItemsAsync(items, RequestSource.ApiGateway, "trace-error");

        // Assert
        result.Should().NotBeNull();
        result.UnifiedItems.Should().HaveCount(1);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static List<Item> CreateValidItems(int count)
    {
        return Enumerable.Range(1, count).Select(i => new Item
        {
            Sku = $"TEST-{i:D3}",
            ProductTitle = "Test Product",
            Description = "Test Description",
            Barcode = "1234567890123",
            SecondaryBarcode = "9876543210987",
            Hts = "1234567890",
            ChinaHts = "6543210987",
            CountryOfOrigin = "US",
            Price = 29.99f,
            Cost = 15.00f,
            LandedCost = 18.00f,
            Size = "M",
            Color = "Blue",
            Brand = "TestBrand",
            ProductType = "Apparel",
            Category = "Clothing",
            Gender = "Unisex",
            FabricContent = "Cotton",
            FabricComposition = "100% Cotton",
            VelocityCode = "V001",
            FastMover = "Y",
            InventorySyncFlag = "ON",
            CreatedAtSnowflake = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)
        }).ToList();
    }

    private static UnifiedItemMaster CreateValidUnifiedItem()
    {
        return new UnifiedItemMaster
        {
            Sku = "TEST-001",
            Name = "Test Product"
        };
    }
}