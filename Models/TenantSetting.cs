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
        public bool LowStockAlertEnabled { get; set; } = true;

        // Staff visibility — Admin controls what Staff users can see
        public bool StaffCanSeeDashboard { get; set; } = true;
        public bool StaffCanSeeFinance { get; set; } = false;
        public bool StaffCanSeeProducts { get; set; } = true;
        public bool StaffCanSeeVariants { get; set; } = true;
        public bool StaffCanSeeTextiles { get; set; } = true;
        public bool StaffCanSeeStockMovement { get; set; } = true;
        public bool StaffCanSeeInventory { get; set; } = true;
        public bool StaffCanSeeSales { get; set; } = true;
        public bool StaffCanSeeCustomers { get; set; } = true;

        // E-commerce settings - only used when the 'storefront' feature code is enabled.
        public bool StorefrontEnabled { get; set; } = false;

        [MaxLength(150)]
        public string? StorefrontTagline { get; set; }

        [MaxLength(1000)]
        public string? StorefrontDescription { get; set; }

        public int? StorefrontShippingFee { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
