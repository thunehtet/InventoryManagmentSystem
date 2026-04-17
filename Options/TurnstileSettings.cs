namespace ClothInventoryApp.Options
{
    public class TurnstileSettings
    {
        public string SiteKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string VerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(SiteKey) &&
            !string.IsNullOrWhiteSpace(SecretKey);
    }
}
