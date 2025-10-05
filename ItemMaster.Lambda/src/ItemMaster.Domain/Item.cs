namespace ItemMaster.Domain;

public sealed class Item
{
    public string Sku { get; }
    public Item(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU cannot be empty", nameof(sku));
        Sku = sku.Trim();
    }
    public override string ToString() => Sku;
}

