using ClothInventoryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<SaleItem> SaleItems => Set<SaleItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProductVariant>()
                .HasIndex(v => v.SKU)
                .IsUnique();

            modelBuilder.Entity<StockMovement>()
                .Property(x => x.MovementType)
                .HasMaxLength(20);
        }
    }
}