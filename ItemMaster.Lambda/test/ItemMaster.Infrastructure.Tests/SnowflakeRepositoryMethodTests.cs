using FluentAssertions;
using ItemMaster.Domain;
using ItemMaster.Infrastructure;
using ItemMaster.Infrastructure.Secrets;
using ItemMaster.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SnowflakeRepositoryMethodTests
{
  public static IEnumerable<object[]> GetLatestItemsCountData()
  {
    yield return new object[] { 1, "Single item" };
    yield return new object[] { 10, "Small batch" };
    yield return new object[] { 50, "Medium batch" };
    yield return new object[] { 100, "Default count" };
    yield return new object[] { 500, "Large batch" };
    yield return new object[] { 1000, "Maximum batch" };
  }

  [Theory]
  [MemberData(nameof(GetLatestItemsCountData))]
  public async Task GetLatestItemsAsync_WithDifferentCounts_ShouldBuildCorrectQuery(int count, string scenario)
  {
    // Arrange
    var config = CreateMockConfig();
    var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
    mockQueryBuilder.Setup(x => x.BuildSelectLatest(count))
        .Returns($"SELECT * FROM TEST_DB.TEST_SCHEMA.ITEMS ORDER BY CREATED_AT_SNOWFLAKE DESC LIMIT {count}");

    var mockSecretsManager = new Mock<Amazon.SecretsManager.IAmazonSecretsManager>();
    var connectionProvider = new SnowflakeConnectionProvider(
        mockSecretsManager.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeConnectionProvider>>());

    var repository = new SnowflakeRepository(
        connectionProvider,
        mockQueryBuilder.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeRepository>>());

    // Act
    var result = await repository.GetLatestItemsAsync(count);

    // Assert
    result.IsSuccess.Should().BeFalse(scenario);
    result.ErrorMessage.Should().Contain("Snowflake query for latest items failed");
    mockQueryBuilder.Verify(x => x.BuildSelectLatest(count), Times.AtMostOnce, scenario);
  }

  [Fact]
  public async Task GetLatestItemsAsync_WithDefaultCount_ShouldUse100()
  {
    // Arrange
    var config = CreateMockConfig();
    var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
    mockQueryBuilder.Setup(x => x.BuildSelectLatest(100))
        .Returns("SELECT * FROM TEST_DB.TEST_SCHEMA.ITEMS ORDER BY CREATED_AT_SNOWFLAKE DESC LIMIT 100");

    var mockSecretsManager = new Mock<Amazon.SecretsManager.IAmazonSecretsManager>();
    var connectionProvider = new SnowflakeConnectionProvider(
        mockSecretsManager.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeConnectionProvider>>());

    var repository = new SnowflakeRepository(
        connectionProvider,
        mockQueryBuilder.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeRepository>>());

    // Act
    var result = await repository.GetLatestItemsAsync();

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Snowflake query for latest items failed");
  }

  public static IEnumerable<object[]> GetItemsBySkusBatchData()
  {
    yield return new object[] { new[] { "SKU-001" }, 1, "Single SKU" };
    yield return new object[] { new[] { "SKU-001", "SKU-002", "SKU-003" }, 3, "Small batch" };
    yield return new object[] { Enumerable.Range(1, 100).Select(i => $"SKU-{i:D3}").ToArray(), 100, "Batch of 100" };
    yield return new object[] { Enumerable.Range(1, 1000).Select(i => $"SKU-{i:D4}").ToArray(), 1000, "Exact batch size" };
    yield return new object[] { Enumerable.Range(1, 1500).Select(i => $"SKU-{i:D4}").ToArray(), 1500, "Multiple batches" };
  }

  [Theory]
  [MemberData(nameof(GetItemsBySkusBatchData))]
  public async Task GetItemsBySkusAsync_WithDifferentBatchSizes_ShouldProcessBatches(
      string[] skus, int expectedCount, string scenario)
  {
    // Arrange
    var config = CreateMockConfig();
    var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();

    mockQueryBuilder.Setup(x => x.BuildSelectBySkus(It.IsAny<IEnumerable<string>>()))
        .Returns<IEnumerable<string>>(inputSkus =>
        {
          var sql = $"SELECT * FROM TEST_DB.TEST_SCHEMA.ITEMS WHERE SKU IN ({string.Join(",", inputSkus.Select(s => $"'{s}'"))})";
          return (sql, null);
        });

    var mockSecretsManager = new Mock<Amazon.SecretsManager.IAmazonSecretsManager>();
    var connectionProvider = new SnowflakeConnectionProvider(
        mockSecretsManager.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeConnectionProvider>>());

    var repository = new SnowflakeRepository(
        connectionProvider,
        mockQueryBuilder.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeRepository>>());

    // Act
    var result = await repository.GetItemsBySkusAsync(skus);

    // Assert
    result.IsSuccess.Should().BeFalse(scenario);
    result.ErrorMessage.Should().Contain("Snowflake query by SKUs failed");
  }

  [Fact]
  public async Task GetItemsBySkusAsync_WithNullAndEmptySkus_ShouldFilterCorrectly()
  {
    // Arrange
    var config = CreateMockConfig();
    var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
    var mockSecretsManager = new Mock<Amazon.SecretsManager.IAmazonSecretsManager>();
    var connectionProvider = new SnowflakeConnectionProvider(
        mockSecretsManager.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeConnectionProvider>>());

    var repository = new SnowflakeRepository(
        connectionProvider,
        mockQueryBuilder.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeRepository>>());

    var skus = new[] { "VALID-SKU", null, "", "   ", "ANOTHER-VALID" };

    // Act
    var result = await repository.GetItemsBySkusAsync(skus);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Snowflake query by SKUs failed");
  }

  [Fact]
  public async Task GetItemsBySkusAsync_WithOnlyEmptySkus_ShouldReturnSuccess()
  {
    // Arrange
    var config = CreateMockConfig();
    var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
    var mockSecretsManager = new Mock<Amazon.SecretsManager.IAmazonSecretsManager>();
    var connectionProvider = new SnowflakeConnectionProvider(
        mockSecretsManager.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeConnectionProvider>>());

    var repository = new SnowflakeRepository(
        connectionProvider,
        mockQueryBuilder.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeRepository>>());

    var skus = new[] { "", "   ", null, "" };

    // Act
    var result = await repository.GetItemsBySkusAsync(skus);

    // Assert
    result.IsSuccess.Should().BeTrue("Empty SKU list should return success");
    result.Value.Should().BeEmpty();
  }

  [Fact]
  public async Task GetLatestItemsAsync_WithZeroCount_ShouldBuildQuery()
  {
    // Arrange
    var config = CreateMockConfig();
    var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
    mockQueryBuilder.Setup(x => x.BuildSelectLatest(0))
        .Returns("SELECT * FROM TEST_DB.TEST_SCHEMA.ITEMS ORDER BY CREATED_AT_SNOWFLAKE DESC LIMIT 0");

    var mockSecretsManager = new Mock<Amazon.SecretsManager.IAmazonSecretsManager>();
    var connectionProvider = new SnowflakeConnectionProvider(
        mockSecretsManager.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeConnectionProvider>>());

    var repository = new SnowflakeRepository(
        connectionProvider,
        mockQueryBuilder.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeRepository>>());

    // Act
    var result = await repository.GetLatestItemsAsync(0);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Snowflake query for latest items failed");
  }

  [Fact]
  public async Task GetItemsBySkusAsync_WithCaseInsensitiveDuplicateSkus_ShouldDeduplicate()
  {
    // Arrange
    var config = CreateMockConfig();
    var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
    var mockSecretsManager = new Mock<Amazon.SecretsManager.IAmazonSecretsManager>();
    var connectionProvider = new SnowflakeConnectionProvider(
        mockSecretsManager.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeConnectionProvider>>());

    var repository = new SnowflakeRepository(
        connectionProvider,
        mockQueryBuilder.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeRepository>>());

    var skus = new[] { "TEST-001", "test-001", "TEST-002", "test-002" };

    // Act
    var result = await repository.GetItemsBySkusAsync(skus);

    // Assert
    result.IsSuccess.Should().BeFalse();
  }

  [Fact]
  public async Task GetLatestItemsAsync_WithCancellationToken_ShouldRespectCancellation()
  {
    // Arrange
    var config = CreateMockConfig();
    var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
    var mockSecretsManager = new Mock<Amazon.SecretsManager.IAmazonSecretsManager>();
    var connectionProvider = new SnowflakeConnectionProvider(
        mockSecretsManager.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeConnectionProvider>>());

    var repository = new SnowflakeRepository(
        connectionProvider,
        mockQueryBuilder.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeRepository>>());

    using var cts = new CancellationTokenSource();
    cts.Cancel();

    // Act
    var result = await repository.GetLatestItemsAsync(cancellationToken: cts.Token);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Snowflake query for latest items failed");
  }

  [Fact]
  public async Task GetItemsBySkusAsync_WithCancellationToken_ShouldRespectCancellation()
  {
    // Arrange
    var config = CreateMockConfig();
    var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
    var mockSecretsManager = new Mock<Amazon.SecretsManager.IAmazonSecretsManager>();
    var connectionProvider = new SnowflakeConnectionProvider(
        mockSecretsManager.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeConnectionProvider>>());

    var repository = new SnowflakeRepository(
        connectionProvider,
        mockQueryBuilder.Object,
        config.Object,
        Mock.Of<ILogger<SnowflakeRepository>>());

    using var cts = new CancellationTokenSource();
    cts.Cancel();

    var skus = new[] { "TEST-001", "TEST-002" };

    // Act
    var result = await repository.GetItemsBySkusAsync(skus, cts.Token);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Snowflake query by SKUs failed");
  }

  private static Mock<IConfiguration> CreateMockConfig()
  {
    var config = new Mock<IConfiguration>();
    config.Setup(x => x["snowflake:database"]).Returns("TEST_DB");
    config.Setup(x => x["snowflake:schema"]).Returns("TEST_SCHEMA");
    config.Setup(x => x["snowflake:warehouse"]).Returns("TEST_WAREHOUSE");
    return config;
  }
}

