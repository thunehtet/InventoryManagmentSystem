namespace ClothInventoryApp.Services.Email
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
    }
}
