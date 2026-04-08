using System;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Brand { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public List<ProductVariant> Variants { get; set; } = new();
}
