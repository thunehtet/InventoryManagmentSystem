using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.Storefront
{
    public class CheckoutDto
    {
        [Required, MaxLength(200), Display(Name = "Full name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required, MaxLength(30), Display(Name = "Phone")]
        public string CustomerPhone { get; set; } = string.Empty;

        [EmailAddress, MaxLength(200), Display(Name = "Email (optional)")]
        public string? CustomerEmail { get; set; }

        [Required, MaxLength(500), Display(Name = "Shipping address")]
        public string ShippingAddress { get; set; } = string.Empty;

        [MaxLength(500), Display(Name = "Order notes (optional)")]
        public string? CustomerNotes { get; set; }
    }
}
