namespace ClothInventoryApp.Services.Files
{
    public static class UploadCategories
    {
        public const string TenantLogo = "TenantLogo";
        public const string UserProfile = "UserProfile";
        public const string SubscriptionPaymentProof = "SubscriptionPaymentProof";
        public const string ProductImage = "ProductImage";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            TenantLogo,
            UserProfile,
            SubscriptionPaymentProof,
            ProductImage
        };
    }
}
