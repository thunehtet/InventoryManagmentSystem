using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dtos.Account
{
    public class LoginDto
    {
        [Required]
        [Display(Name = "Email or Username")]
        public string UserNameOrEmail { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }
    }
}