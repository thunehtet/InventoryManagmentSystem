using System;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string SKU { get; set; } = "";
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";

    public int CostPrice { get; set; }
    public int SellingPrice { get; set; }

    public List<StockMovement> StockMovements { get; set; } = new();
    public List<SaleItem> SaleItems { get; set; } = new();
}
