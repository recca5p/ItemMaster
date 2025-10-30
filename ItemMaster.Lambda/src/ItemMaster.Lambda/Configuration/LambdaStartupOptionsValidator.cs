using Microsoft.Extensions.Options;

namespace ItemMaster.Lambda.Configuration;

public class LambdaStartupOptionsValidator : IValidateOptions<LambdaStartupOptions>
{
    public ValidateOptionsResult Validate(string? name, LambdaStartupOptions options)
    {
        var failures = new List<string>();

        if (!options.TestMode)
        {
            if (string.IsNullOrWhiteSpace(options.Environment))
                failures.Add("Environment is required when not in test mode");

            if (string.IsNullOrWhiteSpace(options.ConfigBase))
                failures.Add("ConfigBase is required when not in test mode");

            if (string.IsNullOrWhiteSpace(options.Region))
                failures.Add("Region is required when not in test mode");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}