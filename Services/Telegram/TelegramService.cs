using ClothInventoryApp.Options;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ClothInventoryApp.Services.Telegram
{
    public class TelegramService : ITelegramService
    {
        private readonly TelegramSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TelegramService> _logger;

        public TelegramService(
            IOptions<TelegramSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<TelegramService> logger)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default)
        {
            var token = ResolveToken();
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
                return false;

            var timeout = Math.Max(5, _settings.TimeoutSeconds);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            var url = $"https://api.telegram.org/bot{token}/sendMessage";
            var payload = new { chat_id = chatId, text };

            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                using var response = await client.PostAsync(url, content, cts.Token);
                var body = await response.Content.ReadAsStringAsync(cts.Token);

                if (response.IsSuccessStatusCode)
                    return true;

                _logger.LogWarning("Telegram sendMessage failed: {Status} {Body}", (int)response.StatusCode, body);
                return false;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Telegram sendMessage timed out for chat {ChatId}.", chatId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram sendMessage failed for chat {ChatId}.", chatId);
                return false;
            }
        }

        private string? ResolveToken() =>
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN"),
                Environment.GetEnvironmentVariable("Telegram__BotToken"),
                _settings.BotToken);

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
}
