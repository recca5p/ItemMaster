namespace ItemMaster.Contracts;

public class ProcessSkusResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ItemsProcessed { get; set; }
    public int ItemsPublished { get; set; }
    public int Failed { get; set; }
}