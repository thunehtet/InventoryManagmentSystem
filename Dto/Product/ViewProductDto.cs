namespace ClothInventoryApp.Dto.Product
{
    public class ViewProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Brand { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }
}
