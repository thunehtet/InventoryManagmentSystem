using ClothInventoryApp.Options;
using Microsoft.Extensions.Options;

namespace ClothInventoryApp.Services.Identity
{
    public class TemporaryCredentialService : ITemporaryCredentialService
    {
        private readonly UserProvisioningSettings _settings;

        public TemporaryCredentialService(IOptions<UserProvisioningSettings> settings)
        {
            _settings = settings.Value;
        }

        public string GenerateTemporaryPassword()
        {
            var prefix = string.IsNullOrWhiteSpace(_settings.TemporaryPasswordPrefix)
                ? "Stockeasy@"
                : _settings.TemporaryPasswordPrefix;

            var digits = Math.Clamp(_settings.TemporaryPasswordDigits, 4, 8);
            var min = (int)Math.Pow(10, digits - 1);
            var maxExclusive = (int)Math.Pow(10, digits);
            var randomNumber = Random.Shared.Next(min, maxExclusive);

            return $"{prefix}{randomNumber}";
        }
    }
}
