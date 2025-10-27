namespace ItemMaster.Lambda.Services;

public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    
    public static ConfigurationValidationResult Success() => new() { IsValid = true };
    public static ConfigurationValidationResult Failure(List<string> errors) => new() { IsValid = false, Errors = errors };
}
