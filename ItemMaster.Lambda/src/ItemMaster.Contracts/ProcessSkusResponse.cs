namespace ItemMaster.Contracts;

public sealed class ProcessSkusResponse
{
    public int Published { get; init; }
    public int Logged { get; init; }
    public int Failed { get; init; }
}

