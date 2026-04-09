using System;

public class Sale
{
    public int Id { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    public int TotalAmount { get; set; }

    public List<SaleItem> Items { get; set; } = new();
}
