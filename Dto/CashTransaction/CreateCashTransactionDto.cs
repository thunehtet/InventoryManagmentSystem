using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.CashTransaction
{
    public class CreateCashTransactionDto
    {
        public Guid Id { get; set; }

        [Display(Name = "Transaction Date")]
        [DataType(DataType.Date)]
        public DateTime TransactionDate { get; set; } = DateTime.Today;

        [Required]
        public string Type { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int Amount { get; set; }

        [Display(Name = "Reference No")]
        public string? ReferenceNo { get; set; }

        public string? Remarks { get; set; }
    }
}