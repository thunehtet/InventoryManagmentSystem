using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly ITenantProvider _tenantProvider;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            ITenantProvider tenantProvider) : base(options)
        {
            _tenantProvider = tenantProvider;
        }

        public Guid CurrentTenantId
        {
            get
            {
                try
                {
                    return _tenantProvider.GetTenantId();
                }
                catch
                {
                    return Guid.Empty;
                }
            }
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<SaleItem> SaleItems => Set<SaleItem>();
        public DbSet<Textile> Textile => Set<Textile>();
        public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<Feature> Features => Set<Feature>();
        public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
        public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
        public DbSet<TenantFeatureOverride> TenantFeatureOverrides => Set<TenantFeatureOverride>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            modelBuilder.Entity<ProductVariant>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            modelBuilder.Entity<StockMovement>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            modelBuilder.Entity<Sale>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            modelBuilder.Entity<SaleItem>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            modelBuilder.Entity<Textile>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            modelBuilder.Entity<CashTransaction>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            modelBuilder.Entity<ProductVariant>()
                .HasIndex(v => v.SKU)
                .IsUnique();

            modelBuilder.Entity<StockMovement>()
                .Property(x => x.MovementType)
                .HasMaxLength(20);

            modelBuilder.Entity<Tenant>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<Plan>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<Feature>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<TenantSetting>()
                .HasOne(x => x.Tenant)
                .WithOne(x => x.TenantSetting)
                .HasForeignKey<TenantSetting>(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TenantSubscription>()
                .HasOne(x => x.Tenant)
                .WithMany(x => x.Subscriptions)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TenantSubscription>()
                .HasOne(x => x.Plan)
                .WithMany(x => x.TenantSubscriptions)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlanFeature>()
                .HasOne(x => x.Plan)
                .WithMany(x => x.PlanFeatures)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlanFeature>()
                .HasOne(x => x.Feature)
                .WithMany(x => x.PlanFeatures)
                .HasForeignKey(x => x.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlanFeature>()
                .HasIndex(x => new { x.PlanId, x.FeatureId })
                .IsUnique();

            modelBuilder.Entity<TenantFeatureOverride>()
                .HasOne(x => x.Tenant)
                .WithMany(x => x.FeatureOverrides)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TenantFeatureOverride>()
                .HasOne(x => x.Feature)
                .WithMany(x => x.TenantFeatureOverrides)
                .HasForeignKey(x => x.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TenantFeatureOverride>()
                .HasIndex(x => new { x.TenantId, x.FeatureId })
                .IsUnique();

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}