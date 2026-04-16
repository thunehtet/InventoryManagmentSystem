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

        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<Feature> Features => Set<Feature>();
        public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
        public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
        public DbSet<TenantFeatureOverride> TenantFeatureOverrides => Set<TenantFeatureOverride>();
        public DbSet<ContactInquiry> ContactInquiries => Set<ContactInquiry>();
        public DbSet<PastSubscription> PastSubscriptions => Set<PastSubscription>();
        public DbSet<CustomerInviteLink> CustomerInviteLinks => Set<CustomerInviteLink>();
        public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();
        public DbSet<SubscriptionPaymentRequest> SubscriptionPaymentRequests => Set<SubscriptionPaymentRequest>();

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

            modelBuilder.Entity<Customer>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            modelBuilder.Entity<CustomerInviteLink>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            modelBuilder.Entity<Customer>()
                .HasOne(c => c.Tenant)
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomerInviteLink>()
                .HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerInviteLink>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProductVariant>()
                .HasOne(v => v.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SaleItem>()
                .HasOne(i => i.ProductVariant)
                .WithMany(v => v.SaleItems)
                .HasForeignKey(i => i.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SaleItem>()
                .HasOne(i => i.Sale)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StockMovement>()
                .HasOne(m => m.ProductVariant)
                .WithMany(v => v.StockMovements)
                .HasForeignKey(m => m.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductVariant>()
                .HasIndex(v => new { v.TenantId, v.SKU })
                .IsUnique();

            modelBuilder.Entity<SaleItem>()
                .Property(i => i.ProductNameSnapshot)
                .HasMaxLength(255);

            modelBuilder.Entity<SaleItem>()
                .Property(i => i.ProductSkuSnapshot)
                .HasMaxLength(255);

            modelBuilder.Entity<SaleItem>()
                .Property(i => i.ProductColorSnapshot)
                .HasMaxLength(255);

            modelBuilder.Entity<SaleItem>()
                .Property(i => i.ProductSizeSnapshot)
                .HasMaxLength(255);

            modelBuilder.Entity<Sale>()
                .HasIndex(x => x.PublicReceiptToken)
                .IsUnique();

            modelBuilder.Entity<CustomerInviteLink>()
                .HasIndex(x => x.Token)
                .IsUnique();

            modelBuilder.Entity<CustomerInviteLink>()
                .HasIndex(x => new { x.TenantId, x.ExpiresAt });

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

            modelBuilder.Entity<UploadedFile>()
                .HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<UploadedFile>()
                .HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<UploadedFile>()
                .HasIndex(x => new { x.Category, x.CreatedAt });

            modelBuilder.Entity<SubscriptionPaymentRequest>()
                .HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubscriptionPaymentRequest>()
                .HasOne(x => x.RequestedByUser)
                .WithMany()
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubscriptionPaymentRequest>()
                .HasOne(x => x.ReviewedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SubscriptionPaymentRequest>()
                .HasOne(x => x.Plan)
                .WithMany()
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubscriptionPaymentRequest>()
                .HasOne(x => x.PaymentProofFile)
                .WithMany()
                .HasForeignKey(x => x.PaymentProofFileId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubscriptionPaymentRequest>()
                .HasOne(x => x.ApprovedSubscription)
                .WithMany()
                .HasForeignKey(x => x.ApprovedSubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SubscriptionPaymentRequest>()
                .HasIndex(x => new { x.TenantId, x.Status, x.SubmittedAt });

            modelBuilder.Entity<ContactInquiry>()
                .Property(x => x.Status)
                .HasMaxLength(20);

            modelBuilder.Entity<ContactInquiry>()
                .HasOne(x => x.ReviewedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ContactInquiry>()
                .HasOne(x => x.ApprovedTenant)
                .WithMany()
                .HasForeignKey(x => x.ApprovedTenantId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ContactInquiry>()
                .HasIndex(x => new { x.Status, x.SubmittedAt });

            // PastSubscription — no query filter (SuperAdmin-only table)
            modelBuilder.Entity<PastSubscription>()
                .HasOne(p => p.Tenant)
                .WithMany()
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PastSubscription>()
                .HasOne(p => p.Plan)
                .WithMany()
                .HasForeignKey(p => p.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PastSubscription>()
                .HasIndex(p => p.TenantId);

            modelBuilder.Entity<PastSubscription>()
                .HasIndex(p => p.ArchivedAt);
        }
    }
}
