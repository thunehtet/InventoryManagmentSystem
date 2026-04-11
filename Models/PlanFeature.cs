namespace ClothInventoryApp.Models
{
    public class PlanFeature
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PlanId { get; set; }
        public Plan Plan { get; set; } = null!;

        public Guid FeatureId { get; set; }
        public Feature Feature { get; set; } = null!;

        public bool IsEnabled { get; set; } = true;
    }
}