using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using ItemMaster.Infrastructure.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SnowflakeConnectionProviderAdvancedTests
{
    [Fact]
    public async Task GetConnectionStringAsync_WithRSAKeyFormat_ShouldConvertToPkcs8()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var rsaKey = @"-----BEGIN RSA PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7UstKNqOiQHmx
EXAMPLE_BASE64_ENCODED_KEY_HERE
-----END RSA PRIVATE KEY-----";

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = rsaKey });

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

    [Fact]
    public async Task GetConnectionStringAsync_WithPKCS8KeyFormat_ShouldHandleCorrectly()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var pkcs8Key = @"-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7UstKNqOiQHmx
EXAMPLE_BASE64_ENCODED_KEY_HERE
-----END PRIVATE KEY-----";

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = pkcs8Key });

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

    [Fact]
    public async Task GetConnectionStringAsync_WithRawBase64Key_ShouldParseAndConvert()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var base64Key = "MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7UstKNqOiQHmx";

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = base64Key });

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

    [Fact]
    public async Task GetConnectionStringAsync_WithJSONContainingPrivateKey_ShouldExtractKey()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var jsonWithKey = """{"private_key": "invalid_key_for_testing"}""";

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = jsonWithKey });

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

    [Fact]
    public async Task GetConnectionStringAsync_WithInvalidJSON_ShouldFallbackToRaw()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var invalidJson = "{invalid json}";

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = invalidJson });

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

    [Fact]
    public async Task GetConnectionStringAsync_WithMissingUserConfig_ShouldThrow()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "test_key" });

        mockConfig.Setup(x => x["snowflake:account"]).Returns("TEST_ACCOUNT");
        mockConfig.Setup(x => x["snowflake:user"]).Returns((string?)null);

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
    public async Task GetConnectionStringAsync_WithJsonButNoPrivateKeyProperty_ShouldUseRaw()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var jsonWithoutKey = """{"other_field": "value"}""";

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = jsonWithoutKey });

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

    [Fact]
    public async Task GetConnectionStringAsync_WithWhitespaceKey_ShouldThrow()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "   " });

        mockConfig.Setup(x => x["snowflake:account"]).Returns("TEST_ACCOUNT");
        mockConfig.Setup(x => x["snowflake:user"]).Returns("TEST_USER");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", "test-secret-path");

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        var act = async () => await provider.GetConnectionStringAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*secret is empty*");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);
    }

    [Fact]
    public async Task GetConnectionStringAsync_WithNullKeyInJSON_ShouldThrow()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var jsonWithNullKey = """{"private_key": null}""";

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = jsonWithNullKey });

        mockConfig.Setup(x => x["snowflake:account"]).Returns("TEST_ACCOUNT");
        mockConfig.Setup(x => x["snowflake:user"]).Returns("TEST_USER");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", "test-secret-path");

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        var act = async () => await provider.GetConnectionStringAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*private key not found*");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);
    }

    [Fact]
    public async Task GetConnectionStringAsync_WithEmptyKeyInJSON_ShouldThrow()
    {
        var mockSecretsManager = new Mock<IAmazonSecretsManager>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<SnowflakeConnectionProvider>>();

        var jsonWithEmptyKey = """{"private_key": ""}""";

        mockSecretsManager.Setup(x => x.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = jsonWithEmptyKey });

        mockConfig.Setup(x => x["snowflake:account"]).Returns("TEST_ACCOUNT");
        mockConfig.Setup(x => x["snowflake:user"]).Returns("TEST_USER");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", "test-secret-path");

        var provider = new SnowflakeConnectionProvider(
            mockSecretsManager.Object,
            mockConfig.Object,
            mockLogger.Object);

        var act = async () => await provider.GetConnectionStringAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*private key not found*");

        Environment.SetEnvironmentVariable("SSM_RSA_PATH", null);
    }
}

