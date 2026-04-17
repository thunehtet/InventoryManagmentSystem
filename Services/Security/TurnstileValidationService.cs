using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClothInventoryApp.Options;
using Microsoft.Extensions.Options;

namespace ClothInventoryApp.Services.Security
{
    public class TurnstileValidationService : ITurnstileValidationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TurnstileSettings _settings;
        private readonly ILogger<TurnstileValidationService> _logger;

        public TurnstileValidationService(
            IHttpClientFactory httpClientFactory,
            IOptions<TurnstileSettings> settings,
            ILogger<TurnstileValidationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> ValidateAsync(string token, string? remoteIp, CancellationToken cancellationToken = default)
        {
            if (!_settings.IsConfigured)
            {
                _logger.LogError("Turnstile validation was requested, but Turnstile is not configured.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(token))
                return false;

            var client = _httpClientFactory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.VerifyUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = _settings.SecretKey,
                    ["response"] = token.Trim(),
                    ["remoteip"] = remoteIp ?? string.Empty
                })
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Turnstile verification failed with status code {StatusCode}.",
                    (int)response.StatusCode);
                return false;
            }

            var payload = await response.Content.ReadFromJsonAsync<TurnstileVerificationResponse>(cancellationToken: cancellationToken);
            if (payload?.Success == true)
                return true;

            if (payload?.ErrorCodes?.Length > 0)
            {
                _logger.LogWarning(
                    "Turnstile verification rejected the request with error codes: {ErrorCodes}",
                    string.Join(", ", payload.ErrorCodes));
            }

            return false;
        }

        private sealed class TurnstileVerificationResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; set; }
        }
    }
}
