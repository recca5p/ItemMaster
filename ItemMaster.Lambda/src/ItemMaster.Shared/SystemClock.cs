namespace ItemMaster.Shared;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}