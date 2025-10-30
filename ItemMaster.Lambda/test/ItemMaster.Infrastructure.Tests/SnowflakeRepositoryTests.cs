using Amazon.SecretsManager;
using FluentAssertions;
using ItemMaster.Infrastructure.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SnowflakeRepositoryTests
{
    [Fact]
    public void Constructor_WithValidConfig_ShouldInitialize()
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

        var mockQueryBuilder = new Mock<ISnowflakeItemQueryBuilder>().Object;
        var mockLogger = new Mock<ILogger<SnowflakeRepository>>();

        var repository = new SnowflakeRepository(
            connectionProvider,
            mockQueryBuilder,
            config.Object,
            mockLogger.Object);

        repository.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithMissingDatabaseConfig_ShouldThrow()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["snowflake:database"]).Returns((string?)null);
        config.Setup(x => x["snowflake:schema"]).Returns("TEST_SCHEMA");
        config.Setup(x => x["snowflake:warehouse"]).Returns("TEST_WAREHOUSE");

        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var connectionProvider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeConnectionProvider>>());

        var act = () => new SnowflakeRepository(
            connectionProvider,
            new Mock<ISnowflakeItemQueryBuilder>().Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeRepository>>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*database*");
    }

    [Fact]
    public void Constructor_WithMissingSchemaConfig_ShouldThrow()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["snowflake:database"]).Returns("TEST_DB");
        config.Setup(x => x["snowflake:schema"]).Returns((string?)null);
        config.Setup(x => x["snowflake:warehouse"]).Returns("TEST_WAREHOUSE");

        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var connectionProvider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeConnectionProvider>>());

        var act = () => new SnowflakeRepository(
            connectionProvider,
            new Mock<ISnowflakeItemQueryBuilder>().Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeRepository>>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*schema*");
    }

    [Fact]
    public void Constructor_WithMissingWarehouseConfig_ShouldThrow()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["snowflake:database"]).Returns("TEST_DB");
        config.Setup(x => x["snowflake:schema"]).Returns("TEST_SCHEMA");
        config.Setup(x => x["snowflake:warehouse"]).Returns((string?)null);

        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var connectionProvider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeConnectionProvider>>());

        var act = () => new SnowflakeRepository(
            connectionProvider,
            new Mock<ISnowflakeItemQueryBuilder>().Object,
            config.Object,
            Mock.Of<ILogger<SnowflakeRepository>>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*warehouse*");
    }
}