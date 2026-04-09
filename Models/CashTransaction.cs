namespace ClothInventoryApp.Models
{
    public class CashTransaction
    {
        public Guid Id { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Now;
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public int Amount { get; set; }
        public string ReferenceNo { get; set; } = "";
        public string Remarks { get; set; } = "";
    }
}
