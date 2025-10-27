using Amazon;
using Amazon.Extensions.NETCore.Setup;
using ItemMaster.Lambda.Configuration;
using Microsoft.Extensions.Configuration;

namespace ItemMaster.Lambda.Services;

public interface IConfigurationService
{
    LambdaStartupOptions GetStartupOptions();
    IConfiguration BuildConfiguration(LambdaStartupOptions options);
}

public class ConfigurationService : IConfigurationService
{
    public LambdaStartupOptions GetStartupOptions()
    {
        var testMode = Environment.GetEnvironmentVariable(ConfigurationConstants.ITEMMASTER_TEST_MODE)
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        var options = new LambdaStartupOptions
        {
            TestMode = testMode
        };

        if (!testMode)
        {
            options.Environment = Environment.GetEnvironmentVariable(ConfigurationConstants.DOTNET_ENVIRONMENT)
                                  ?? throw new InvalidOperationException(
                                      $"{ConfigurationConstants.DOTNET_ENVIRONMENT} environment variable is required");

            options.ConfigBase = Environment.GetEnvironmentVariable(ConfigurationConstants.CONFIG_BASE)
                                 ?? throw new InvalidOperationException(
                                     $"{ConfigurationConstants.CONFIG_BASE} environment variable is required");

            options.Region = Environment.GetEnvironmentVariable(ConfigurationConstants.REGION)
                             ?? throw new InvalidOperationException(
                                 $"{ConfigurationConstants.REGION} environment variable is required");
        }

        var applyMigrations = Environment.GetEnvironmentVariable(ConfigurationConstants.APPLY_MIGRATIONS)
                              ?? Environment.GetEnvironmentVariable(ConfigurationConstants.APPLY_MIGATIONS);
        options.ApplyMigrations = string.Equals(applyMigrations, "true", StringComparison.OrdinalIgnoreCase);

        return options;
    }

    public IConfiguration BuildConfiguration(LambdaStartupOptions options)
    {
        if (options.TestMode)
            return new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

        var envLower = options.Environment.ToLowerInvariant();
        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true)
            .AddJsonFile($"appsettings.{options.Environment}.json", true)
            .AddEnvironmentVariables()
            .AddSystemsManager(src =>
            {
                src.Path = $"{options.ConfigBase}/{envLower}/";
                src.AwsOptions = new AWSOptions { Region = RegionEndpoint.GetBySystemName(options.Region) };
                src.Optional = true;
                src.ReloadAfter = TimeSpan.FromMinutes(5);
            })
            .Build();
    }
}