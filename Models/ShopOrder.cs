using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class ShopOrder
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        public string OrderNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string PublicToken { get; set; } = string.Empty;

        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [Required, MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string CustomerPhone { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? CustomerEmail { get; set; }

        [Required, MaxLength(500)]
        public string ShippingAddress { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? CustomerNotes { get; set; }

        // Financial
        public int Subtotal { get; set; }
        public int Discount { get; set; }
        public int ShippingFee { get; set; }
        public int TotalAmount { get; set; }

        // Lifecycle: Pending | Confirmed | Shipped | Delivered | Cancelled
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending";

        // Payment: Unpaid | Paid
        [Required, MaxLength(20)]
        public string PaymentStatus { get; set; } = "Unpaid";

        // Link to Sale created when the tenant confirms the order (optional until confirmation)
        public Guid? SaleId { get; set; }
        public Sale? Sale { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        [MaxLength(500)]
        public string? InternalNotes { get; set; }

        public List<ShopOrderItem> Items { get; set; } = new();
    }
}
