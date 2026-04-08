using System;

public class StockMovement
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;

    public string MovementType { get; set; } = ""; // IN, OUT, ADJUST
    public int Quantity { get; set; }
    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
    public string Remarks { get; set; } = "";
}
