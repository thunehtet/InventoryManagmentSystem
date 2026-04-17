using System.Net;
using System.Net.Mail;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClothInventoryApp.Options;
using Microsoft.Extensions.Options;

namespace ClothInventoryApp.Services.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;

        public SmtpEmailService(IOptions<EmailSettings> settings, IHttpClientFactory httpClientFactory)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            var provider = FirstNonEmpty(
                Environment.GetEnvironmentVariable("Email__Provider"),
                Environment.GetEnvironmentVariable("EMAIL_PROVIDER"),
                _settings.Provider)
                ?? "Auto";

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
                Environment.GetEnvironmentVariable("Email__FromName"),
                Environment.GetEnvironmentVariable("EMAIL_FROM_NAME"),
                _settings.FromName)
                ?? "StockEasy";

            var enableSsl = FirstBool(
                Environment.GetEnvironmentVariable("Email__EnableSsl"),
                Environment.GetEnvironmentVariable("EMAIL_ENABLE_SSL"),
                _settings.EnableSsl);

            var port = FirstInt(
                Environment.GetEnvironmentVariable("Email__Port"),
                Environment.GetEnvironmentVariable("EMAIL_PORT"),
                _settings.Port);

            var timeoutSeconds = Math.Max(
                3,
                FirstInt(
                    Environment.GetEnvironmentVariable("Email__TimeoutSeconds"),
                    Environment.GetEnvironmentVariable("EMAIL_TIMEOUT_SECONDS"),
                    _settings.TimeoutSeconds));

            var resendApiKey = FirstNonEmpty(
                Environment.GetEnvironmentVariable("RESEND_API_KEY"),
                Environment.GetEnvironmentVariable("Email__ResendApiKey"),
                Environment.GetEnvironmentVariable("EMAIL_RESEND_API_KEY"));

            var resendApiBaseUrl = FirstNonEmpty(
                Environment.GetEnvironmentVariable("Email__ResendApiBaseUrl"),
                Environment.GetEnvironmentVariable("EMAIL_RESEND_API_BASE_URL"),
                _settings.ResendApiBaseUrl)
                ?? "https://api.resend.com";

            if (string.IsNullOrWhiteSpace(to))
                throw new InvalidOperationException("Recipient email is required.");

            var transport = ResolveProvider(provider, resendApiKey);
            if (transport == "resendapi")
            {
                await SendWithResendApiAsync(
                    resendApiBaseUrl,
                    resendApiKey,
                    fromAddress,
                    fromName,
                    to,
                    subject,
                    htmlBody,
                    timeoutSeconds,
                    cancellationToken);
                return;
            }

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

        private async Task SendWithResendApiAsync(
            string apiBaseUrl,
            string? apiKey,
            string? fromAddress,
            string fromName,
            string to,
            string subject,
            string htmlBody,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromAddress))
                throw new InvalidOperationException("Resend API settings are incomplete.");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.BaseAddress = new Uri(AppendTrailingSlash(apiBaseUrl));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                from = $"{fromName} <{fromAddress}>",
                to = new[] { to },
                subject,
                html = htmlBody
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            throw new InvalidOperationException(
                $"Resend API send failed with status {(int)response.StatusCode}: {body}");
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static int FirstInt(string? first, string? second, int fallback)
        {
            foreach (var value in new[] { first, second })
            {
                if (int.TryParse(value, out var parsed))
                    return parsed;
            }

            return fallback;
        }

        private static bool FirstBool(string? first, string? second, bool fallback)
        {
            foreach (var value in new[] { first, second })
            {
                if (bool.TryParse(value, out var parsed))
                    return parsed;
            }

            return fallback;
        }

        private static string ResolveProvider(string provider, string? resendApiKey)
        {
            if (string.Equals(provider, "ResendApi", StringComparison.OrdinalIgnoreCase))
                return "resendapi";

            if (string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
                return "smtp";

            return !string.IsNullOrWhiteSpace(resendApiKey)
                ? "resendapi"
                : "smtp";
        }

        private static string AppendTrailingSlash(string value)
        {
            return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
        }
    }
}
