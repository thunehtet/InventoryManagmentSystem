namespace ClothInventoryApp.Options
{
    public class TelegramSettings
    {
        public string BotToken { get; set; } = string.Empty;
        public string BotUsername { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 10;
    }
}
