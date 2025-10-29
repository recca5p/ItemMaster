using System.Diagnostics.CodeAnalysis;

namespace ItemMaster.Shared;

[ExcludeFromCodeCoverage]
public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}