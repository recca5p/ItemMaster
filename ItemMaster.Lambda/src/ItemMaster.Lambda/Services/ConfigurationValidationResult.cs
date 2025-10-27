namespace ItemMaster.Lambda.Services;

public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ConfigurationValidationResult Success()
    {
        return new ConfigurationValidationResult { IsValid = true };
    }

    public static ConfigurationValidationResult Failure(List<string> errors)
    {
        return new ConfigurationValidationResult { IsValid = false, Errors = errors };
    }
}