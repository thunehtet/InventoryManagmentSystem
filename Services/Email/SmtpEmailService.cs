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
            if (string.IsNullOrWhiteSpace(to))
                throw new InvalidOperationException("Recipient email is required.");

            if (string.IsNullOrWhiteSpace(_settings.Host) ||
                string.IsNullOrWhiteSpace(_settings.UserName) ||
                string.IsNullOrWhiteSpace(_settings.Password) ||
                string.IsNullOrWhiteSpace(_settings.FromAddress))
            {
                throw new InvalidOperationException("Email settings are incomplete.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.UserName, _settings.Password),
                EnableSsl = _settings.EnableSsl
            };

            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, cancellationToken);
        }
    }
}
