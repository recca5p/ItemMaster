namespace ItemMaster.Shared;

public interface IClock
{
    DateTime UtcNow { get; }
}