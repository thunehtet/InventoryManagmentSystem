namespace ClothInventoryApp.Services.Telegram
{
    public interface ITelegramService
    {
        Task<bool> SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default);
    }
}
