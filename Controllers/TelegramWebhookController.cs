using ClothInventoryApp.Data;
using ClothInventoryApp.Services.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ClothInventoryApp.Controllers
{
    /// <summary>
    /// Receives Telegram Bot webhook updates.
    /// When a user clicks the deep link and taps START, the bot receives
    /// /start <token>. We match the token to a StockEasy user and save their chat_id.
    /// </summary>
    [AllowAnonymous]
    [ApiController]
    [Route("telegram/webhook")]
    public class TelegramWebhookController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITelegramService _telegram;
        private readonly ILogger<TelegramWebhookController> _logger;

        public TelegramWebhookController(
            AppDbContext db,
            ITelegramService telegram,
            ILogger<TelegramWebhookController> logger)
        {
            _db = db;
            _telegram = telegram;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Receive(CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(cancellationToken);

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (!root.TryGetProperty("message", out var message))
                    return Ok();

                if (!message.TryGetProperty("chat", out var chat) ||
                    !chat.TryGetProperty("id", out var chatIdProp))
                    return Ok();

                var chatId = chatIdProp.GetInt64().ToString();

                var text = message.TryGetProperty("text", out var textProp)
                    ? textProp.GetString() ?? string.Empty
                    : string.Empty;

                var firstName = message.TryGetProperty("from", out var from) &&
                                from.TryGetProperty("first_name", out var fn)
                    ? fn.GetString() ?? "there"
                    : "there";

                if (text.StartsWith("/start ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = text["/start ".Length..].Trim();
                    await HandleLinkTokenAsync(token, chatId, firstName, cancellationToken);
                }
                else if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
                {
                    await _telegram.SendMessageAsync(chatId,
                        $"👋 Hello {firstName}!\n\n" +
                        "To connect your StockEasy account, go to your Profile in the app and click \"Connect Telegram\".",
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Telegram webhook.");
            }

            return Ok();
        }

        private async Task HandleLinkTokenAsync(
            string token, string chatId, string firstName, CancellationToken ct)
        {
            var linkToken = await _db.TelegramLinkTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == token && t.ExpiresAt > DateTime.UtcNow, ct);

            if (linkToken == null)
            {
                await _telegram.SendMessageAsync(chatId,
                    "❌ This link has expired or is invalid.\n\nPlease go back to your StockEasy Profile and generate a new link.",
                    ct);
                return;
            }

            linkToken.User.TelegramChatId = chatId;
            _db.TelegramLinkTokens.Remove(linkToken);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Telegram linked for user {UserId} → chat {ChatId}.", linkToken.UserId, chatId);

            await _telegram.SendMessageAsync(chatId,
                $"✅ Hello {firstName}!\n\n" +
                $"Your Telegram is now connected to StockEasy account \"{linkToken.User.FullName}\".\n\n" +
                "You will now receive notifications for:\n" +
                "• Low stock alerts\n" +
                "• Subscription expiry warnings\n" +
                "• Payment approval / rejection",
                ct);
        }
    }
}
