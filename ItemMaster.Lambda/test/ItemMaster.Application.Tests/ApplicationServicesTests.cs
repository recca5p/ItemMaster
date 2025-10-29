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

public class ProcessingResponseBuilderTests
{
    private readonly ProcessingResponseBuilder _builder;

    public ProcessingResponseBuilderTests()
    {
        _builder = new ProcessingResponseBuilder();
    }

    [Fact]
    public void CreateSuccessResponse_WithItemsAndSkips_ShouldCalculateCorrectStats()
    {
        // Arrange
        var items = CreateTestItems(5);
        var notFoundSkus = new List<string> { "SKU-999" };
        var mappingResult = new ItemMappingResult
        {
            UnifiedItems = items.Select(i => new UnifiedItemMaster { Sku = i.Sku, Name = "Test" }).ToList(),
            SkippedItems = new List<SkippedItemDetail>
            {
                new() { Sku = "SKU-888", Reason = "Validation failed" }
            },
            SuccessfulSkus = items.Select(i => i.Sku).ToList(),
            PublishedItems = new List<PublishedItemDetail>()
        };

        // Act
        var result = _builder.CreateSuccessResponse(items, notFoundSkus, mappingResult);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsProcessed.Should().Be(5);
        result.ItemsPublished.Should().Be(5);
        result.Failed.Should().Be(1);
    }

    private static List<Item> CreateTestItems(int count)
    {
        return Enumerable.Range(1, count).Select(i => new Item
        {
            Sku = $"SKU-{i:D3}",
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

public class SkuAnalysisServiceTests
{
    private readonly Mock<ILogger<SkuAnalysisService>> _mockLogger;
    private readonly SkuAnalysisService _service;

    public SkuAnalysisServiceTests()
    {
        _mockLogger = new Mock<ILogger<SkuAnalysisService>>();
        _service = new SkuAnalysisService(_mockLogger.Object);
    }

    [Fact]
    public void AnalyzeNotFoundSkus_WithAllFound_ShouldReturnEmpty()
    {
        // Arrange
        var requested = new List<string> { "SKU-001", "SKU-002", "SKU-003" };
        var found = CreateTestItems(3);

        // Act
        var result = _service.AnalyzeNotFoundSkus(requested, found);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeNotFoundSkus_WithSomeNotFound_ShouldReturnMissingSkus()
    {
        // Arrange
        var requested = new List<string> { "SKU-001", "SKU-002", "SKU-999" };
        var found = CreateTestItems(2);

        // Act
        var result = _service.AnalyzeNotFoundSkus(requested, found);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("SKU-999");
    }

    [Fact]
    public void AnalyzeNotFoundSkus_WithNoRequested_ShouldReturnEmpty()
    {
        // Arrange
        var requested = new List<string>();
        var found = CreateTestItems(3);

        // Act
        var result = _service.AnalyzeNotFoundSkus(requested, found);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeNotFoundSkus_WithCaseInsensitiveMatching_ShouldMatchCorrectly()
    {
        // Arrange
        var requested = new List<string> { "sku-001", "SKU-002", "sku-999" };
        var found = CreateTestItems(2);

        // Act
        var result = _service.AnalyzeNotFoundSkus(requested, found);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("sku-999");
    }

    [Fact]
    public void AnalyzeNotFoundSkus_WithNotFoundSkus_ShouldLogWarning()
    {
        // Arrange
        var requested = new List<string> { "SKU-999", "SKU-998" };
        var found = CreateTestItems(1);

        // Act
        _service.AnalyzeNotFoundSkus(requested, found);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static List<Item> CreateTestItems(int count)
    {
        return Enumerable.Range(1, count).Select(i => new Item
        {
            Sku = $"SKU-{i:D3}",
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

