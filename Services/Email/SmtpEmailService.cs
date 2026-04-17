using System.Net;
using System.Net.Mail;
using ClothInventoryApp.Options;
using Microsoft.Extensions.Options;

namespace ClothInventoryApp.Services.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public SmtpEmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            var host = FirstNonEmpty(
                _settings.Host,
                Environment.GetEnvironmentVariable("Email__Host"),
                Environment.GetEnvironmentVariable("EMAIL_HOST"));

            var userName = FirstNonEmpty(
                _settings.UserName,
                Environment.GetEnvironmentVariable("Email__UserName"),
                Environment.GetEnvironmentVariable("EMAIL_USERNAME"),
                string.Equals(host, "smtp.resend.com", StringComparison.OrdinalIgnoreCase) ? "resend" : null);

            var password = FirstNonEmpty(
                _settings.Password,
                Environment.GetEnvironmentVariable("Email__Password"),
                Environment.GetEnvironmentVariable("EMAIL_PASSWORD"),
                Environment.GetEnvironmentVariable("RESEND_API_KEY"));

            var fromAddress = FirstNonEmpty(
                _settings.FromAddress,
                Environment.GetEnvironmentVariable("Email__FromAddress"),
                Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS"),
                Environment.GetEnvironmentVariable("RESEND_FROM_ADDRESS"));

            var fromName = FirstNonEmpty(
                _settings.FromName,
                Environment.GetEnvironmentVariable("Email__FromName"),
                Environment.GetEnvironmentVariable("EMAIL_FROM_NAME"))
                ?? "StockEasy";

            var enableSsl = FirstBool(
                _settings.EnableSsl,
                Environment.GetEnvironmentVariable("Email__EnableSsl"),
                Environment.GetEnvironmentVariable("EMAIL_ENABLE_SSL"));

            var port = FirstInt(
                _settings.Port,
                Environment.GetEnvironmentVariable("Email__Port"),
                Environment.GetEnvironmentVariable("EMAIL_PORT"));

            var timeoutSeconds = Math.Max(
                3,
                FirstInt(
                    _settings.TimeoutSeconds,
                    Environment.GetEnvironmentVariable("Email__TimeoutSeconds"),
                    Environment.GetEnvironmentVariable("EMAIL_TIMEOUT_SECONDS")));

            if (string.IsNullOrWhiteSpace(to))
                throw new InvalidOperationException("Recipient email is required.");

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromAddress))
            {
                throw new InvalidOperationException("Email settings are incomplete.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(userName, password),
                EnableSsl = enableSsl,
                Timeout = timeoutSeconds * 1000
            };

            cancellationToken.ThrowIfCancellationRequested();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await client.SendMailAsync(message).WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"SMTP send timed out after {timeoutSeconds} seconds.");
            }
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static int FirstInt(int fallback, params string?[] values)
        {
            foreach (var value in values)
            {
                if (int.TryParse(value, out var parsed))
                    return parsed;
            }

            return fallback;
        }

        private static bool FirstBool(bool fallback, params string?[] values)
        {
            foreach (var value in values)
            {
                if (bool.TryParse(value, out var parsed))
                    return parsed;
            }

            return fallback;
        }
    }
}
