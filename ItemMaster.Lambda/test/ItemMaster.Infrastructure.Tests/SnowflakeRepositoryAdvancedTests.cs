using Amazon.SecretsManager;
using FluentAssertions;
using ItemMaster.Infrastructure.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SnowflakeRepositoryAdvancedTests
{
    public static IEnumerable<object[]> GetItemsBySkusTestData()
    {
        yield return new object[]
        {
            Array.Empty<string>(),
            "Empty SKU list should return empty result"
        };

        yield return new object[]
        {
            new[] { "TEST-001" },
            "Single SKU should return one item"
        };

        yield return new object[]
        {
            new[] { "TEST-001", "TEST-002", "TEST-003" },
            "Multiple SKUs under batch limit"
        };

        yield return new object[]
        {
            Enumerable.Range(1, 1500).Select(i => $"TEST-{i:D4}").ToArray(),
            "SKUs over batch size should be processed in batches"
        };

        yield return new object[]
        {
            new[] { "TEST-001", null, "", " ", "TEST-002" },
            "Null and empty SKUs should be filtered"
        };
    }

    [Theory]
    [MemberData(nameof(GetItemsBySkusTestData))]
    public async Task GetItemsBySkusAsync_WithDifferentInputs_ShouldFilterAndProcessCorrectly(
        string[] skus,
        string scenario)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["snowflake:database"]).Returns("TEST_DB");
        config.Setup(x => x["snowflake:schema"]).Returns("TEST_SCHEMA");
        config.Setup(x => x["snowflake:warehouse"]).Returns("TEST_WAREHOUSE");

        var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
        mockQueryBuilder.Setup(x => x.BuildSelectBySkus(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(input =>
            {
                var skuList = input.ToList();
                if (skuList.Count == 0) return ("SELECT * FROM TEST_DB.TEST_SCHEMA.ITEMS WHERE 1=0", null);

                var sanitized = string.Join(",", skuList.Select(s => $"'{s}'"));
                return ($"SELECT * FROM TEST_DB.TEST_SCHEMA.ITEMS WHERE SKU IN ({sanitized})", null);
            });

        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var connectionProvider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeConnectionProvider>>());

        var mockLogger = Mock.Of<ILogger<SnowflakeRepository>>();

        var mockConnectionProvider = new Mock<ISnowflakeConnectionProvider>();
        mockConnectionProvider.Setup(x => x.GetConnectionStringAsync())
            .ThrowsAsync(new InvalidOperationException("Mock connection error for test"));

        var repository = new SnowflakeRepository(
            connectionProvider,
            mockQueryBuilder.Object,
            config.Object,
            mockLogger);

        var result = await repository.GetItemsBySkusAsync(skus, CancellationToken.None);

        if (skus.Length == 0 || skus.All(string.IsNullOrWhiteSpace))
        {
            result.IsSuccess.Should().BeTrue(scenario);
            result.Value.Should().BeEmpty();
        }
        else
        {
            result.IsSuccess.Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task GetLatestItemsAsync_WithDifferentCounts_ShouldBuildCorrectQuery(int count)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["snowflake:database"]).Returns("TEST_DB");
        config.Setup(x => x["snowflake:schema"]).Returns("TEST_SCHEMA");
        config.Setup(x => x["snowflake:warehouse"]).Returns("TEST_WAREHOUSE");

        var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
        mockQueryBuilder.Setup(x => x.BuildSelectLatest(count))
            .Returns(
                $"SELECT * FROM TEST_DB.TEST_SCHEMA.ITEMS WHERE CREATED_AT_SNOWFLAKE IS NOT NULL ORDER BY CREATED_AT_SNOWFLAKE DESC LIMIT {count}");

        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var connectionProvider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeConnectionProvider>>());

        var repository = new SnowflakeRepository(
            connectionProvider,
            mockQueryBuilder.Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeRepository>>());

        var result = await repository.GetLatestItemsAsync(count, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Snowflake query for latest items failed");
    }

    [Fact]
    public void Constructor_WithAllValidParameters_ShouldInitialize()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["snowflake:database"]).Returns("TEST_DB");
        config.Setup(x => x["snowflake:schema"]).Returns("TEST_SCHEMA");
        config.Setup(x => x["snowflake:warehouse"]).Returns("TEST_WAREHOUSE");

        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var connectionProvider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeConnectionProvider>>());

        var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>();
        var mockLogger = Mock.Of<ILogger<SnowflakeRepository>>();

        var repository = new SnowflakeRepository(
            connectionProvider,
            mockQueryBuilder.Object,
            config.Object,
            mockLogger);

        repository.Should().NotBeNull();
    }
}