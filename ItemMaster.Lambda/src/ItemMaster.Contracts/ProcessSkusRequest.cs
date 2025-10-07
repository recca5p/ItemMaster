namespace ItemMaster.Contracts;

public sealed class ProcessSkusRequest
{
    public List<string> Skus { get; set; } = new();
}

