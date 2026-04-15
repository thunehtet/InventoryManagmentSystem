namespace ClothInventoryApp.Dto.Customer
{
    public class CustomerInviteLinkDto
    {
        public Guid Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public bool IsExpired { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public string? CustomerName { get; set; }
    }
}
