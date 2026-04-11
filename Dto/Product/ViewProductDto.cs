namespace ClothInventoryApp.Dto.Product
{
    public class ViewProductDto
    {
        public Guid Id { get; set; }
        public Guid TenantId {  get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Brand { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }
}
