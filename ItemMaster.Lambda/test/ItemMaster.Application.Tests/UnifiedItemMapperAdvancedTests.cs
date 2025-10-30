using FluentAssertions;
using ItemMaster.Domain;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Application.Tests;

public class UnifiedItemMapperAdvancedTests
{
    private readonly UnifiedItemMapper _mapper;
    private readonly Mock<ILogger<UnifiedItemMapper>> _mockLogger;

    public UnifiedItemMapperAdvancedTests()
    {
        _mockLogger = new Mock<ILogger<UnifiedItemMapper>>();
        _mapper = new UnifiedItemMapper(_mockLogger.Object);
    }

    [Fact]
    public void MapToUnifiedModel_WithValidItemCreatedAfter2024_ShouldUseBarcodeAsGs1Barcode()
    {
        // Arrange
        var item = CreateValidItem();
        item.CreatedAtSnowflake = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.Gs1Barcode.Should().Be(item.Barcode);
        result.UnifiedItem!.AlternateBarcodes.Should().Contain(item.SecondaryBarcode);
        result.UnifiedItem!.AlternateBarcodes.Should().Contain(item.ThirdBarcode);
    }

    [Fact]
    public void MapToUnifiedModel_WithValidItemCreatedBefore2024_ShouldUseSecondaryBarcodeAsGs1Barcode()
    {
        // Arrange
        var item = CreateValidItem();
        item.CreatedAtSnowflake = new DateTimeOffset(2023, 12, 31, 10, 30, 0, TimeSpan.Zero);

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.Gs1Barcode.Should().Be(item.SecondaryBarcode);
        result.UnifiedItem!.AlternateBarcodes.Should().Contain(item.Barcode);
    }

    [Fact]
    public void MapToUnifiedModel_WithNullCreatedAtSnowflake_ShouldUseSecondaryBarcodeAsGs1Barcode()
    {
        // Arrange
        var item = CreateValidItem();
        item.CreatedAtSnowflake = null;

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.Gs1Barcode.Should().Be(item.SecondaryBarcode);
        result.UnifiedItem!.AlternateBarcodes.Should().Contain(item.Barcode);
    }

    [Fact]
    public void MapToUnifiedModel_WithValidHts_ShouldExtractHsCommodityCode()
    {
        // Arrange
        var item = CreateValidItem();
        item.Hts = "1234567890";

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.HsCommodityCode.Should().Be("123456");
        result.UnifiedItem!.HtsTariffCode.Should().Be("1234567890");
    }

    [Fact]
    public void MapToUnifiedModel_WithShortHts_ShouldNotExtractHsCommodityCode()
    {
        // Arrange
        var item = CreateValidItem();
        item.Hts = "123";

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("Invalid HTS code"));
    }

    [Fact]
    public void MapToUnifiedModel_WithOptionalFields_ShouldIncludeAllData()
    {
        // Arrange
        var item = CreateValidItem();
        item.Description = "Test Description";
        item.ChinaHts = "6543210987";
        item.Gender = "Unisex";
        item.VelocityCode = "V001";
        item.FastMover = "Y";

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.Description.Should().Be("Test Description");
        result.UnifiedItem!.ChinaHtsCode.Should().Be("6543210987");
        result.UnifiedItem!.Attributes.Should().Contain(a => a.Id == "gender" && a.Value == "Unisex");
        result.UnifiedItem!.Attributes.Should().Contain(a => a.Id == "velocity_code" && a.Value == "V001");
        result.UnifiedItem!.Attributes.Should().Contain(a => a.Id == "fast_mover" && a.Value == "Y");
    }

    [Fact]
    public void MapToUnifiedModel_WithAllThreeBarcodes_ShouldIncludeInAlternateBarcodes()
    {
        // Arrange
        var item = CreateValidItem();
        item.CreatedAtSnowflake = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.AlternateBarcodes.Should().HaveCount(2);
        result.UnifiedItem!.AlternateBarcodes.Should().Contain(item.SecondaryBarcode);
        result.UnifiedItem!.AlternateBarcodes.Should().Contain(item.ThirdBarcode);
    }

    [Fact]
    public void MapToUnifiedModel_WithCategories_ShouldMapCorrectly()
    {
        // Arrange
        var item = CreateValidItem();
        item.ProductType = "Apparel";
        item.Category = "Clothing";

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.Categories.Should().HaveCount(2);
        result.UnifiedItem!.Categories.Should().Contain(c => c.Path == "Apparel" && c.Source == "brand");
        result.UnifiedItem!.Categories.Should().Contain(c => c.Path == "Clothing" && c.Source == "aka");
    }

    [Fact]
    public void MapToUnifiedModel_WithImages_ShouldMapAllPositions()
    {
        // Arrange
        var item = CreateValidItem();

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.Images.Should().HaveCount(3);
        result.UnifiedItem!.Images.Should().OnlyContain(img => img.SizeType == "original_size");
    }

    [Fact]
    public void MapToUnifiedModel_WithDates_ShouldMapBothShopifyAndSnowflake()
    {
        // Arrange
        var item = CreateValidItem();

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.Dates.Should().HaveCount(2);
        result.UnifiedItem!.Dates.Should().Contain(d => d.System == "shopify");
        result.UnifiedItem!.Dates.Should().Contain(d => d.System == "snowflake");
    }

    [Fact]
    public void MapToUnifiedModel_WithInventorySyncFlag_ShouldDefaultToON()
    {
        // Arrange
        var item = CreateValidItem();
        item.InventorySyncFlag = string.Empty;

        // Act
        var result = _mapper.MapToUnifiedModel(item);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UnifiedItem!.Attributes.Should().Contain(a => a.Id == "inventory_sync_enabled" && a.Value == "ON");
    }

    private static Item CreateValidItem()
    {
        return new Item
        {
            Sku = "TEST-001",
            ProductTitle = "Test Product",
            Description = "Test Description",
            Barcode = "1234567890123",
            SecondaryBarcode = "9876543210987",
            ThirdBarcode = "1112223334445",
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
            ProductImageUrl = "https://example.com/image.jpg",
            ProductImageUrlPos1 = "https://example.com/pos1.jpg",
            ProductImageUrlPos2 = "https://example.com/pos2.jpg",
            ProductImageUrlPos3 = "https://example.com/pos3.jpg",
            CreatedAtShopify = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
            CreatedAtSnowflake = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
            UpdatedAtSnowflake = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)
        };
    }
}