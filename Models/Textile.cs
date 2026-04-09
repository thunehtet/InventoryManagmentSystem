namespace ClothInventoryApp.Models
{
    public class Textile
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string PurchaseFrom { get; set; } = "";
        public int Quantity { get; set; }
        public DateTime PurchaseDate { get; set; }
        public int UnitPrice {  get; set; }

        public int TotalPrice {  get; set; }
    }
}
