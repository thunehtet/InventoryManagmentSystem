namespace ClothInventoryApp.Dto.Textile
{
    public class ViewTextileDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PurchaseFrom { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime PurchaseDate { get; set; }
        public int UnitPrice { get; set; }
        public int TotalPrice { get; set; }
    }
}