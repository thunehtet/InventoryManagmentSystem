namespace ClothInventoryApp.Dto.Order
{
    public class OrderDetailsDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string PublicToken { get; set; } = string.Empty;

        public Guid? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string ShippingAddress { get; set; } = string.Empty;
        public string? CustomerNotes { get; set; }
        public string? InternalNotes { get; set; }

        public int Subtotal { get; set; }
        public int Discount { get; set; }
        public int ShippingFee { get; set; }
        public int TotalAmount { get; set; }

        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;

        public Guid? SaleId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public Guid Id { get; set; }
        public Guid? ProductVariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int LineTotal { get; set; }
    }
}
