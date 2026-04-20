namespace ClothInventoryApp.Dto.CashTransaction
{
    public class ViewCashTransactionDto
    {
        public Guid Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string? ReferenceNo { get; set; }
        public string? Remarks { get; set; }
    }
}
