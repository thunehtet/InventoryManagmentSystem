namespace ClothInventoryApp.Dto.Subscription
{
    public class PlanLimitWarningDto
    {
        public string ResourceName { get; set; } = string.Empty;
        public int Current { get; set; }
        public int Max { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
