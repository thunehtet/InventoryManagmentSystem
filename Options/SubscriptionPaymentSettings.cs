namespace ClothInventoryApp.Options
{
    public class SubscriptionPaymentSettings
    {
        public string WalletDisplayName { get; set; } = string.Empty;
        public string WalletAccountName { get; set; } = string.Empty;
        public string WalletAccountNo { get; set; } = string.Empty;
        public string QrImageUrl { get; set; } = string.Empty;
        public string Instructions { get; set; } = "Pay the subscription fee, keep your screenshot, and submit the last 6 transaction digits for review.";
    }
}
