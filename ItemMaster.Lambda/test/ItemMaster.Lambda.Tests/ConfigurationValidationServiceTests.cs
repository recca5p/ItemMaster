using FluentAssertions;
using ItemMaster.Lambda.Configuration;
using ItemMaster.Lambda.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ItemMaster.Lambda.Tests;

public class ConfigurationValidationServiceTests
{
    private readonly ConfigurationValidationService _service;

    public ConfigurationValidationServiceTests()
    {
        _service = new ConfigurationValidationService();
    }

    [Fact]
    public void ValidateConfiguration_WithValidConfig_ShouldReturnSuccess()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var result = _service.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfiguration_WithMissingSqsUrl_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            { ConfigurationConstants.SQS_URL, "" }
        });

        // Act
        var result = _service.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Missing SQS URL configuration");
    }

    [Fact]
    public void ValidateConfiguration_WithMissingSnowflakeDatabase_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            { ConfigurationConstants.SNOWFLAKE_DATABASE, "" }
        });

        // Act
        var result = _service.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Missing Snowflake database configuration");
    }

    [Fact]
    public void ValidateConfiguration_WithMissingSnowflakeSchema_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            { ConfigurationConstants.SNOWFLAKE_SCHEMA, "" }
        });

        // Act
        var result = _service.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Missing Snowflake schema configuration");
    }

    [Fact]
    public void ValidateConfiguration_WithMissingSnowflakeTable_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            { ConfigurationConstants.SNOWFLAKE_TABLE, "" }
        });

        // Act
        var result = _service.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Missing Snowflake table configuration");
    }

    [Fact]
    public void ValidateConfiguration_WithMissingSnowflakeWarehouse_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            { ConfigurationConstants.SNOWFLAKE_WAREHOUSE, "" }
        });

        // Act
        var result = _service.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Missing Snowflake warehouse configuration");
    }

    [Fact]
    public void ValidateConfiguration_WithMultipleMissingFields_ShouldReturnAllErrors()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string>
        {
            { ConfigurationConstants.SQS_URL, "" },
            { ConfigurationConstants.SNOWFLAKE_DATABASE, "" },
            { ConfigurationConstants.SNOWFLAKE_SCHEMA, "" }
        });

        // Act
        var result = _service.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain("Missing SQS URL configuration");
        result.Errors.Should().Contain("Missing Snowflake database configuration");
        result.Errors.Should().Contain("Missing Snowflake schema configuration");
    }

    private static IConfiguration CreateValidConfiguration()
    {
        return CreateConfiguration(new Dictionary<string, string>
        {
            { ConfigurationConstants.SQS_URL, "https://sqs.ap-southeast-1.amazonaws.com/123456789/test-queue" },
            { ConfigurationConstants.SNOWFLAKE_DATABASE, "WAREHOUSE_DB" },
            { ConfigurationConstants.SNOWFLAKE_SCHEMA, "PUBLIC" },
            { ConfigurationConstants.SNOWFLAKE_TABLE, "ITEMS" },
            { ConfigurationConstants.SNOWFLAKE_WAREHOUSE, "COMPUTE_WH" }
        });
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string> overrides)
    {
        var baseConfig = new Dictionary<string, string>
        {
            { ConfigurationConstants.SQS_URL, "https://sqs.ap-southeast-1.amazonaws.com/123456789/test-queue" },
            { ConfigurationConstants.SNOWFLAKE_DATABASE, "WAREHOUSE_DB" },
            { ConfigurationConstants.SNOWFLAKE_SCHEMA, "PUBLIC" },
            { ConfigurationConstants.SNOWFLAKE_TABLE, "ITEMS" },
            { ConfigurationConstants.SNOWFLAKE_WAREHOUSE, "COMPUTE_WH" }
        };

        foreach (var kvp in overrides) baseConfig[kvp.Key] = kvp.Value;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(baseConfig.Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value)))
            .Build();
    }
}