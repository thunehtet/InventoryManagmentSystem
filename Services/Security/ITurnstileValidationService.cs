namespace ClothInventoryApp.Services.Security
{
    public interface ITurnstileValidationService
    {
        Task<bool> ValidateAsync(string token, string? remoteIp, CancellationToken cancellationToken = default);
    }
}
