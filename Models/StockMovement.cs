using ClothInventoryApp.Models;

public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string MovementType { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
    public string? Remarks { get; set; }
    // Nullable link back to the sale that generated this movement (for cascade delete)
    public Guid? SaleId { get; set; }
}