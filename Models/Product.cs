using System;
using ClothInventoryApp.Models;

public class Product
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Brand { get; set; } = "";
    public bool IsActive { get; set; } = true;

    // E-commerce fields - optional public shop fallback content.
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }

    public List<ProductVariant> Variants { get; set; } = new();
}
