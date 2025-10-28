using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using ItemMaster.Infrastructure.Secrets;
using ItemMaster.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Secrets;

public class SecretsAwareMySqlConnectionStringProviderTests
{
    public static IEnumerable<object[]> GetConnectionStringTestData()
    {
        yield return new object[]
        {
            "ap-southeast-1.amazonaws.com",
            "itemmaster_db",
            "arn:aws:secretsmanager:ap-southeast-1:123456789:secret:mysql-secret",
            """{"Username":"dbuser","Password":"dbpass"}""",
            false,
            "Valid configuration with JSON secret (capitalized properties)"
        };
    }

    [Theory]
    [MemberData(nameof(GetConnectionStringTestData))]
    public async Task GetMySqlConnectionStringAsync_WithValidConfig_ShouldReturnConnectionString(
        string host,
        string database,
        string secretArn,
        string secretJson,
        bool shouldFail,
        string scenario)
    {
        // Arrange
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SecretsAwareMySqlConnectionStringProvider>>();

        mockConfig.Setup(x => x["mysql:host"]).Returns(host);
        mockConfig.Setup(x => x["mysql:db"]).Returns(database);
        mockConfig.Setup(x => x["mysql:secret_arn"]).Returns(secretArn);

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = secretJson });

        var provider = new SecretsAwareMySqlConnectionStringProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        // Act
        if (shouldFail)
        {
            var act = async () => await provider.GetMySqlConnectionStringAsync();
            await act.Should().ThrowAsync<InvalidOperationException>(scenario);
        }
        else
        {
            var result = await provider.GetMySqlConnectionStringAsync();

            // Assert
            result.Should().NotBeNullOrEmpty(scenario);
            result.Should().Contain($"Server={host}");
            result.Should().Contain($"Database={database}");
        }
    }

    [Fact]
    public async Task GetMySqlConnectionStringAsync_WithMissingHost_ShouldThrow()
    {
        // Arrange
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SecretsAwareMySqlConnectionStringProvider>>();

        mockConfig.Setup(x => x["mysql:host"]).Returns((string?)null);
        mockConfig.Setup(x => x["mysql:db"]).Returns("db");
        mockConfig.Setup(x => x["mysql:secret_arn"]).Returns("arn");

        var provider = new SecretsAwareMySqlConnectionStringProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        // Act
        var act = async () => await provider.GetMySqlConnectionStringAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MySQL configuration*");
    }

    [Fact]
    public async Task GetMySqlConnectionStringAsync_WithMissingDatabase_ShouldThrow()
    {
        // Arrange
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SecretsAwareMySqlConnectionStringProvider>>();

        mockConfig.Setup(x => x["mysql:host"]).Returns("host");
        mockConfig.Setup(x => x["mysql:db"]).Returns((string?)null);
        mockConfig.Setup(x => x["mysql:secret_arn"]).Returns("arn");

        var provider = new SecretsAwareMySqlConnectionStringProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        // Act
        var act = async () => await provider.GetMySqlConnectionStringAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MySQL configuration*");
    }

    [Fact]
    public async Task GetMySqlConnectionStringAsync_WithMissingSecretArn_ShouldThrow()
    {
        // Arrange
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SecretsAwareMySqlConnectionStringProvider>>();

        mockConfig.Setup(x => x["mysql:host"]).Returns("host");
        mockConfig.Setup(x => x["mysql:db"]).Returns("db");
        mockConfig.Setup(x => x["mysql:secret_arn"]).Returns((string?)null);

        var provider = new SecretsAwareMySqlConnectionStringProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        // Act
        var act = async () => await provider.GetMySqlConnectionStringAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MySQL configuration*");
    }

    [Fact]
    public async Task GetMySqlConnectionStringAsync_WithEmptySecret_ShouldThrow()
    {
        // Arrange
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SecretsAwareMySqlConnectionStringProvider>>();

        mockConfig.Setup(x => x["mysql:host"]).Returns("ap-southeast-1.amazonaws.com");
        mockConfig.Setup(x => x["mysql:db"]).Returns("itemmaster");
        mockConfig.Setup(x => x["mysql:secret_arn"]).Returns("arn:aws:secretsmanager:ap-southeast-1:123456789:secret:mysql");

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "" });

        var provider = new SecretsAwareMySqlConnectionStringProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        // Act
        var act = async () => await provider.GetMySqlConnectionStringAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Empty secret*");
    }

    [Fact]
    public void Constructor_WithAllParameters_ShouldInitialize()
    {
        // Arrange
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SecretsAwareMySqlConnectionStringProvider>>();

        // Act
        var provider = new SecretsAwareMySqlConnectionStringProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        // Assert
        provider.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMySqlConnectionStringAsync_WithSecretsManagerError_ShouldLogAndThrow()
    {
        // Arrange
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SecretsAwareMySqlConnectionStringProvider>>();

        mockConfig.Setup(x => x["mysql:host"]).Returns("ap-southeast-1.amazonaws.com");
        mockConfig.Setup(x => x["mysql:db"]).Returns("itemmaster");
        mockConfig.Setup(x => x["mysql:secret_arn"]).Returns("arn:aws:secretsmanager:ap-southeast-1:123456789:secret:mysql");

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("AWS error"));

        var provider = new SecretsAwareMySqlConnectionStringProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        // Act
        var act = async () => await provider.GetMySqlConnectionStringAsync();
        await act.Should().ThrowAsync<Exception>();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}

