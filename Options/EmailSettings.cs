namespace ClothInventoryApp.Options
{
    public class EmailSettings
    {
        public string Provider { get; set; } = "Auto";
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "StockEasy";
        public bool EnableSsl { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 10;
        public string ResendApiBaseUrl { get; set; } = "https://api.resend.com";
    }
}
