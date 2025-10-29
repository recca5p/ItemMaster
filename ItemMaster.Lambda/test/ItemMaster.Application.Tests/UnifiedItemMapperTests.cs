using FluentAssertions;
using ItemMaster.Application;
using ItemMaster.Contracts;
using ItemMaster.Domain;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Application.Tests;

public class UnifiedItemMapperTests
{
  private readonly Mock<ILogger<UnifiedItemMapper>> _mockLogger;
  private readonly UnifiedItemMapper _mapper;

  public UnifiedItemMapperTests()
  {
    _mockLogger = new Mock<ILogger<UnifiedItemMapper>>();
    _mapper = new UnifiedItemMapper(_mockLogger.Object);
  }

  [Fact]
  public void MapToUnifiedModel_WithValidItem_ShouldReturnSuccess()
  {
    // Arrange
    var item = CreateValidItem();

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.UnifiedItem.Should().NotBeNull();
    result.UnifiedItem!.Sku.Should().Be("TEST-001");
  }

  [Theory]
  [InlineData("", "Missing SKU")]
  [InlineData(null, "Missing SKU")]
  [InlineData("   ", "Missing SKU")]
  public void MapToUnifiedModel_WithMissingSku_ShouldReturnFailure(string sku, string expectedError)
  {
    // Arrange
    var item = CreateValidItem();
    item.Sku = sku;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain(expectedError);
  }

  [Theory]
  [InlineData("", "Missing ProductTitle (Name)")]
  [InlineData(null, "Missing ProductTitle (Name)")]
  [InlineData("   ", "Missing ProductTitle (Name)")]
  public void MapToUnifiedModel_WithMissingProductTitle_ShouldReturnFailure(string title, string expectedError)
  {
    // Arrange
    var item = CreateValidItem();
    item.ProductTitle = title;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain(expectedError);
  }

  [Fact]
  public void MapToUnifiedModel_WithMissingBothBarcodes_ShouldReturnFailure()
  {
    // Arrange
    var item = CreateValidItem();
    item.Barcode = string.Empty;
    item.SecondaryBarcode = string.Empty;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain("Missing both Barcode and SecondaryBarcode");
  }

  [Theory]
  [InlineData("123456789", "Invalid HTS code (must be 10 digits, got: '123456789')")]
  [InlineData("12345678901", "Invalid HTS code (must be 10 digits, got: '12345678901')")]
  [InlineData("", "Invalid HTS code (must be 10 digits, got: '')")]
  [InlineData(null, "Invalid HTS code (must be 10 digits, got: '')")]
  public void MapToUnifiedModel_WithInvalidHts_ShouldReturnFailure(string hts, string expectedError)
  {
    // Arrange
    var item = CreateValidItem();
    item.Hts = hts;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain(expectedError);
  }

  [Theory]
  [InlineData("U", "Invalid CountryOfOrigin (must be 2 chars, got: 'U')")]
  [InlineData("USA", "Invalid CountryOfOrigin (must be 2 chars, got: 'USA')")]
  [InlineData("", "Invalid CountryOfOrigin (must be 2 chars, got: '')")]
  [InlineData(null, "Invalid CountryOfOrigin (must be 2 chars, got: '')")]
  public void MapToUnifiedModel_WithInvalidCountryOfOrigin_ShouldReturnFailure(string country, string expectedError)
  {
    // Arrange
    var item = CreateValidItem();
    item.CountryOfOrigin = country;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain(expectedError);
  }

  [Theory]
  [InlineData("", "Missing Color")]
  [InlineData(null, "Missing Color")]
  [InlineData("   ", "Missing Color")]
  public void MapToUnifiedModel_WithMissingColor_ShouldReturnFailure(string color, string expectedError)
  {
    // Arrange
    var item = CreateValidItem();
    item.Color = color;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain(expectedError);
  }

  [Theory]
  [InlineData("", "Missing Size")]
  [InlineData(null, "Missing Size")]
  [InlineData("   ", "Missing Size")]
  public void MapToUnifiedModel_WithMissingSize_ShouldReturnFailure(string size, string expectedError)
  {
    // Arrange
    var item = CreateValidItem();
    item.Size = size;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain(expectedError);
  }

  [Fact]
  public void MapToUnifiedModel_WithMissingLandedCost_ShouldReturnFailure()
  {
    // Arrange
    var item = CreateValidItem();
    item.LandedCost = 0;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain("Missing required LandedCost");
  }

  [Fact]
  public void MapToUnifiedModel_WithMissingFabricContent_ShouldReturnFailure()
  {
    // Arrange
    var item = CreateValidItem();
    item.FabricContent = string.Empty;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain("Missing required FabricContent");
  }

  [Fact]
  public void MapToUnifiedModel_WithMissingFabricComposition_ShouldReturnFailure()
  {
    // Arrange
    var item = CreateValidItem();
    item.FabricComposition = string.Empty;

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ValidationErrors.Should().Contain("Missing required FabricComposition");
  }

  [Fact]
  public void MapToUnifiedModel_WithAllRequiredFields_ShouldMapCorrectly()
  {
    // Arrange
    var item = CreateValidItem();

    // Act
    var result = _mapper.MapToUnifiedModel(item);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.UnifiedItem!.Sku.Should().Be("TEST-001");
    result.UnifiedItem!.Name.Should().Be("Test Product");
    result.UnifiedItem!.HtsTariffCode.Should().Be("1234567890");
    result.UnifiedItem!.CountryOfOriginCode.Should().Be("US");
    result.UnifiedItem!.Prices.Should().HaveCount(1);
    result.UnifiedItem!.Prices[0].Type.Should().Be("list");
    result.UnifiedItem!.Prices[0].Currency.Should().Be("USD");
    result.UnifiedItem!.Costs.Should().HaveCountGreaterThan(0);
    result.UnifiedItem!.Attributes.Should().Contain(a => a.Id == "fabric_content" && a.Value == "Cotton");
    result.UnifiedItem!.Attributes.Should().Contain(a => a.Id == "fabric_composition" && a.Value == "100% Cotton");
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

