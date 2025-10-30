using ItemMaster.Lambda.Configuration;
using Microsoft.Extensions.Configuration;

namespace ItemMaster.Lambda.Services;

public interface IConfigurationValidationService
{
    ConfigurationValidationResult ValidateConfiguration(IConfiguration configuration);
}

public class ConfigurationValidationService : IConfigurationValidationService
{
    public ConfigurationValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var errors = new List<string>();

        var sqsUrl = configuration[ConfigurationConstants.SQS_URL];
        if (string.IsNullOrWhiteSpace(sqsUrl)) errors.Add("Missing SQS URL configuration");

        var sfDb = configuration[ConfigurationConstants.SNOWFLAKE_DATABASE];
        var sfSchema = configuration[ConfigurationConstants.SNOWFLAKE_SCHEMA];
        var sfTable = configuration[ConfigurationConstants.SNOWFLAKE_TABLE];
        var sfWarehouse = configuration[ConfigurationConstants.SNOWFLAKE_WAREHOUSE];

        if (string.IsNullOrWhiteSpace(sfDb))
            errors.Add("Missing Snowflake database configuration");

        if (string.IsNullOrWhiteSpace(sfSchema))
            errors.Add("Missing Snowflake schema configuration");

        if (string.IsNullOrWhiteSpace(sfTable))
            errors.Add("Missing Snowflake table configuration");

        if (string.IsNullOrWhiteSpace(sfWarehouse))
            errors.Add("Missing Snowflake warehouse configuration");

        return errors.Any()
            ? ConfigurationValidationResult.Failure(errors)
            : ConfigurationValidationResult.Success();
    }
}