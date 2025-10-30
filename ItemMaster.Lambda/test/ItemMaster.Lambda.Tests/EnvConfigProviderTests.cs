using FluentAssertions;
using ItemMaster.Shared;
using Xunit;

namespace ItemMaster.Lambda.Tests;

public class EnvConfigProviderTests
{
    private readonly EnvConfigProvider _provider;

    public EnvConfigProviderTests()
    {
        _provider = new EnvConfigProvider();
    }

    [Fact]
    public void GetConfigValue_WithExistingEnvironmentVariable_ShouldReturnValue()
    {
        // Arrange
        var key = "TEST_ENV_VAR";
        var expectedValue = "test-value";
        Environment.SetEnvironmentVariable(key, expectedValue);

        try
        {
            // Act
            var result = _provider.GetConfigValue(key);

            // Assert
            result.Should().Be(expectedValue);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GetConfigValue_WithNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        var key = "NON_EXISTENT_VAR";
        Environment.SetEnvironmentVariable(key, null);

        // Act
        var result = _provider.GetConfigValue(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetConfigValue_WithEmptyValue_ShouldReturnEmptyString()
    {
        // Arrange
        var key = "EMPTY_VAR";
        Environment.SetEnvironmentVariable(key, "");

        try
        {
            // Act
            var result = _provider.GetConfigValue(key);

            // Assert
            result.Should().BeNullOrEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}