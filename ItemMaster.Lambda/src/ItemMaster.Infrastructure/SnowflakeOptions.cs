using System.Diagnostics.CodeAnalysis;

namespace ItemMaster.Infrastructure;

[ExcludeFromCodeCoverage]
public class SnowflakeOptions
{
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
}