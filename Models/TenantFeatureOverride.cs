namespace ClothInventoryApp.Models
{
    public class TenantFeatureOverride
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Guid FeatureId { get; set; }
        public Feature Feature { get; set; } = null!;

        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}