using ClothInventoryApp.Models;

public class ProductVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string SKU { get; set; } = "";
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";

    public int CostPrice { get; set; }
    public int SellingPrice { get; set; }

    public List<ProductVariantImage> Images { get; set; } = new();
    public List<StockMovement> StockMovements { get; set; } = new();
    public List<SaleItem> SaleItems { get; set; } = new();
}
