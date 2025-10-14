namespace ItemMaster.Contracts;

public class ItemForSqs
{
    public string Sku { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public float Price { get; set; }
}