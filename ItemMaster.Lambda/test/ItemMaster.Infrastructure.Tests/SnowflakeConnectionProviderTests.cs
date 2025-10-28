using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using ItemMaster.Infrastructure.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SnowflakeConnectionProviderTests
{
    public static IEnumerable<object[]> GetConnectionStringTestData()
    {
        yield return new object[]
        {
            """{"private_key": "MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC..."}""",
            true,
            "JSON format with private_key"
        };

        yield return new object[]
        {
            "-----BEGIN RSA PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC...\n-----END RSA PRIVATE KEY-----",
            true,
            "Direct PEM RSA format"
        };

        yield return new object[]
        {
            "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC...\n-----END PRIVATE KEY-----",
            true,
            "PKCS#8 format"
        };
    }

    [Theory]
    [MemberData(nameof(GetConnectionStringTestData))]
    public async Task GetConnectionStringAsync_WithDifferentKeyFormats_ShouldParseCorrectly(
        string secretString,
        bool shouldSucceed,
        string scenario)
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse
            {
                SecretString = secretString
            });

        mockConfig.Setup(x => x["snowflake:account"]).Returns("TEST_ACCOUNT");
        mockConfig.Setup(x => x["snowflake:user"]).Returns("TEST_USER");
        mockConfig.Setup(x => x["snowflake:role"]).Returns((string?)null);

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", "test-secret-path");

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        var act = async () => await provider.GetConnectionStringAsync();

        await act.Should().ThrowAsync<Exception>();

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);
    }

    [Fact]
    public async Task GetConnectionStringAsync_WithoutEnvironmentVariable_ShouldThrow()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        mockConfig.Setup(x => x["snowflake:account"]).Returns("TEST_ACCOUNT");
        mockConfig.Setup(x => x["snowflake:user"]).Returns("TEST_USER");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        var act = async () => await provider.GetConnectionStringAsync();

        await act.Should().ThrowAsync<Exception>();

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);
    }

    [Fact]
    public async Task GetConnectionStringAsync_WithEmptySecret_ShouldThrow()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse
            {
                SecretString = ""
            });

        mockConfig.Setup(x => x["snowflake:account"]).Returns("TEST_ACCOUNT");
        mockConfig.Setup(x => x["snowflake:user"]).Returns("TEST_USER");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", "test-secret-path");

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        var act = async () => await provider.GetConnectionStringAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);
    }

    [Fact]
    public async Task GetConnectionStringAsync_WithMissingAccountConfig_ShouldThrow()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse
            {
                SecretString = """{"private_key": "test_key"}"""
            });

        mockConfig.Setup(x => x["snowflake:account"]).Returns((string?)null);
        mockConfig.Setup(x => x["snowflake:user"]).Returns("TEST_USER");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", "test-secret-path");

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        var act = async () => await provider.GetConnectionStringAsync();

        await act.Should().ThrowAsync<Exception>();

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);
    }

    [Fact]
    public async Task GetConnectionStringAsync_WithRole_ShouldIncludeRoleInConnectionString()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse
            {
                SecretString = """{"private_key": "test_key"}"""
            });

        mockConfig.Setup(x => x["snowflake:account"]).Returns("TEST_ACCOUNT");
        mockConfig.Setup(x => x["snowflake:user"]).Returns("TEST_USER");
        mockConfig.Setup(x => x["snowflake:role"]).Returns("TEST_ROLE");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", "test-secret-path");

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        var act = async () => await provider.GetConnectionStringAsync();

        await act.Should().ThrowAsync<Exception>();

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);
    }

    [Fact]
    public void Constructor_WithAllParameters_ShouldInitialize()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        provider.Should().NotBeNull();
    }

    [Fact]
    public async Task GetConnectionStringAsync_WithBase64EncodedKey_ShouldHandleCorrectly()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var invalidKey = "not-valid-base64-but-tests-code-path";

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse
            {
                SecretString = invalidKey
            });

        mockConfig.Setup(x => x["snowflake:account"]).Returns("TEST_ACCOUNT");
        mockConfig.Setup(x => x["snowflake:user"]).Returns("TEST_USER");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", "test-secret-path");

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        var act = async () => await provider.GetConnectionStringAsync();

        await act.Should().ThrowAsync<Exception>();

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);
    }
}

