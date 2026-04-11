using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class TenantSetting
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [MaxLength(20)]
        public string? PrimaryColor { get; set; }

        [MaxLength(20)]
        public string? SecondaryColor { get; set; }

        public bool ShowFinanceModule { get; set; } = false;
        public bool ShowInventoryModule { get; set; } = true;
        public bool ShowSalesModule { get; set; } = true;
        public bool ShowReportsModule { get; set; } = false;

        [MaxLength(20)]
        public string? InvoicePrefix { get; set; }

        [MaxLength(20)]
        public string? OrderPrefix { get; set; }

        public int? LowStockThreshold { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}