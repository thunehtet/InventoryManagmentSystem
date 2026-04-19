using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.TenantSettings
{
    public class TenantSettingsEditDto
    {
        // Low stock
        [Range(1, 10000)]
        public int? LowStockThreshold { get; set; }
        public bool LowStockAlertEnabled { get; set; }

        // Staff visibility
        public bool StaffCanSeeDashboard { get; set; }
        public bool StaffCanSeeFinance { get; set; }
        public bool StaffCanSeeProducts { get; set; }
        public bool StaffCanSeeVariants { get; set; }
        public bool StaffCanSeeTextiles { get; set; }
        public bool StaffCanSeeStockMovement { get; set; }
        public bool StaffCanSeeInventory { get; set; }
        public bool StaffCanSeeSales { get; set; }
        public bool StaffCanSeeCustomers { get; set; }
    }
}
