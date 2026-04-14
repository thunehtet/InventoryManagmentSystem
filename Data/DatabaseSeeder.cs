using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Data
{
    public static class DatabaseSeeder
    {
        // ── System tenant GUID — hardcoded, never changes ────────────
        public static readonly Guid SystemTenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000001");

        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var env         = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

            await SeedSuperAdminAsync(context, userManager);
            await SeedPlansAndFeaturesAsync(context);   // always runs — idempotent

            // Demo data only in development
            if (env.IsDevelopment())
                await SeedDemoDataAsync(context, userManager);
        }

        // ── Always runs — creates the System tenant + SuperAdmin user ─
        private static async Task SeedSuperAdminAsync(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            // Idempotent: skip if SuperAdmin already exists
            if (await userManager.Users.AnyAsync(u => u.IsSuperAdmin))
                return;

            // 1. Create the System tenant (invisible to all business users)
            var systemTenant = new Tenant
            {
                Id           = SystemTenantId,
                Code         = "SYSTEM",
                Name         = "System",
                IsActive     = false,     // not a real business tenant
                CreatedAt    = DateTime.UtcNow
            };
            context.Tenants.Add(systemTenant);
            await context.SaveChangesAsync();

            // 2. Create SuperAdmin user
            var superAdmin = new ApplicationUser
            {
                UserName        = "superadmin@stockeasy.com",
                Email           = "superadmin@stockeasy.com",
                EmailConfirmed  = true,
                FullName        = "Super Admin",
                TenantId        = SystemTenantId,
                IsSuperAdmin    = true,
                IsTenantAdmin   = false,
                IsActive        = true,
                Type            = "SuperAdmin",
                MustChangePassword = false,
                CreatedAt       = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(superAdmin, "SuperAdmin@2026");
            if (!result.Succeeded)
                throw new Exception("SuperAdmin seed failed: " +
                    string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // ── Always runs — idempotent plan + feature catalogue ────────
        private static async Task SeedPlansAndFeaturesAsync(AppDbContext context)
        {
            // Skip entirely if plans already exist
            if (await context.Plans.IgnoreQueryFilters().AnyAsync())
                return;

            // ── Plans ────────────────────────────────────────────────────
            var planFree = new Plan
            {
                Name = "Free", Code = "FREE",
                Description = "Get started at no cost — products, variants and basic sales included.",
                PriceMonthly = 0,
                MaxUsers = 1, MaxProducts = 5, MaxVariants = 10,
                IsActive = true
            };
            var planStarter = new Plan
            {
                Name = "Starter", Code = "STARTER",
                Description = "For small shops — full sales with profit tracking and customer management.",
                PriceMonthly = 9, PriceYearly = 90,
                MaxUsers = 3, MaxProducts = 10, MaxVariants = 50,
                IsActive = true
            };
            var planPro = new Plan
            {
                Name = "Pro", Code = "PRO",
                Description = "Full features for growing businesses — dashboard, finance and textiles.",
                PriceMonthly = 25, PriceYearly = 249,
                MaxUsers = 10, MaxProducts = 100, MaxVariants = 300,
                IsActive = true
            };
            context.Plans.AddRange(planFree, planStarter, planPro);

            // ── Features ─────────────────────────────────────────────────
            var featSales      = new Feature { Code = "sales",       Name = "Sales",               Description = "Record and view sales transactions",                      IsActive = true };
            var featSaleProfit = new Feature { Code = "sale_profit",  Name = "Sale Profit & Customer", Description = "Profit, discount, cost columns + customer assignment in sales", IsActive = true };
            var featCustomers  = new Feature { Code = "customers",    Name = "Customer Management", Description = "Manage customers and link them to sales",                IsActive = true };
            var featDashboard  = new Feature { Code = "dashboard",    Name = "Dashboard & Analytics", Description = "Sales trends, profit charts and stock analytics",      IsActive = true };
            var featFinance    = new Feature { Code = "finance",      Name = "Finance Dashboard",   Description = "Cash flow, profit charts, expense tracking",             IsActive = true };
            var featTextiles   = new Feature { Code = "textiles",     Name = "Textile / Materials", Description = "Raw material purchase tracking",                        IsActive = true };
            context.Features.AddRange(featSales, featSaleProfit, featCustomers, featDashboard, featFinance, featTextiles);
            await context.SaveChangesAsync();

            // ── Plan → Feature mapping ────────────────────────────────────
            // Free: sales only (basic — no profit/customer columns)
            context.PlanFeatures.Add(new PlanFeature { PlanId = planFree.Id, FeatureId = featSales.Id, IsEnabled = true });

            // Starter: sales + profit columns + customers
            context.PlanFeatures.AddRange(
                new PlanFeature { PlanId = planStarter.Id, FeatureId = featSales.Id,      IsEnabled = true },
                new PlanFeature { PlanId = planStarter.Id, FeatureId = featSaleProfit.Id, IsEnabled = true },
                new PlanFeature { PlanId = planStarter.Id, FeatureId = featCustomers.Id,  IsEnabled = true }
            );

            // Pro: everything
            context.PlanFeatures.AddRange(
                new PlanFeature { PlanId = planPro.Id, FeatureId = featSales.Id,      IsEnabled = true },
                new PlanFeature { PlanId = planPro.Id, FeatureId = featSaleProfit.Id, IsEnabled = true },
                new PlanFeature { PlanId = planPro.Id, FeatureId = featCustomers.Id,  IsEnabled = true },
                new PlanFeature { PlanId = planPro.Id, FeatureId = featDashboard.Id,  IsEnabled = true },
                new PlanFeature { PlanId = planPro.Id, FeatureId = featFinance.Id,    IsEnabled = true },
                new PlanFeature { PlanId = planPro.Id, FeatureId = featTextiles.Id,   IsEnabled = true }
            );
            await context.SaveChangesAsync();
        }

        // ── Runs once — demo business tenants (dev only) ─────────────
        private static async Task SeedDemoDataAsync(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            // Skip if any real (non-System) tenant already exists
            if (await context.Tenants.IgnoreQueryFilters()
                    .AnyAsync(t => t.Code != "SYSTEM"))
                return;

            // Plans and features already seeded by SeedPlansAndFeaturesAsync —
            // look them up by code for use in demo tenant subscriptions.
            var planFree    = await context.Plans.IgnoreQueryFilters().FirstAsync(p => p.Code == "FREE");
            var planStarter = await context.Plans.IgnoreQueryFilters().FirstAsync(p => p.Code == "STARTER");
            var planPro     = await context.Plans.IgnoreQueryFilters().FirstAsync(p => p.Code == "PRO");

            // ════════════════════════════════════════════════════════════
            //  TENANT 1 — ThreadCo (Pro plan)
            // ════════════════════════════════════════════════════════════
            var t1 = new Tenant
            {
                Code = "THREADCO", Name = "ThreadCo",
                ContactEmail = "info@threadco.com", Country = "United States",
                CurrencyCode = "USD", IsActive = true, CreatedAt = DateTime.UtcNow
            };
            context.Tenants.Add(t1);
            await context.SaveChangesAsync();

            context.TenantSettings.Add(new TenantSetting
            {
                TenantId = t1.Id,
                ShowFinanceModule = true, ShowInventoryModule = true,
                ShowSalesModule = true, ShowReportsModule = true,
                LowStockThreshold = 5, InvoicePrefix = "TC", CreatedAt = DateTime.UtcNow
            });
            context.TenantSubscriptions.Add(new TenantSubscription
            {
                TenantId = t1.Id, PlanId = planPro.Id,          // Pro — all features
                StartDate = DateTime.UtcNow.AddMonths(-2), EndDate = DateTime.UtcNow.AddMonths(10),
                BillingCycle = "Monthly", Price = planPro.PriceMonthly, IsActive = true
            });
            await context.SaveChangesAsync();

            // Users
            await userManager.CreateAsync(new ApplicationUser
            {
                UserName = "admin@threadco.com", Email = "admin@threadco.com", EmailConfirmed = true,
                FullName = "ThreadCo Admin", TenantId = t1.Id,
                IsTenantAdmin = true, IsActive = true, Type = "Admin", CreatedAt = DateTime.UtcNow
            }, "Admin@1234");

            await userManager.CreateAsync(new ApplicationUser
            {
                UserName = "staff@threadco.com", Email = "staff@threadco.com", EmailConfirmed = true,
                FullName = "ThreadCo Staff", TenantId = t1.Id,
                IsTenantAdmin = false, IsActive = true, Type = "Staff", CreatedAt = DateTime.UtcNow
            }, "Staff@1234");

            // Products
            var p1 = new Product { Id = Guid.NewGuid(), TenantId = t1.Id, Name = "Oxford Shirt",    Category = "Shirts",   Brand = "ClassicWear", IsActive = true };
            var p2 = new Product { Id = Guid.NewGuid(), TenantId = t1.Id, Name = "Chino Trousers",  Category = "Trousers", Brand = "UrbanFit",    IsActive = true };
            var p3 = new Product { Id = Guid.NewGuid(), TenantId = t1.Id, Name = "Linen Blazer",    Category = "Blazers",  Brand = "EleganceX",   IsActive = true };
            context.Products.AddRange(p1, p2, p3);
            await context.SaveChangesAsync();

            // Variants
            var v1a = new ProductVariant { TenantId = t1.Id, ProductId = p1.Id, SKU = "TC-OXF-S-WHT",  Size = "S",  Color = "White",    CostPrice = 18, SellingPrice = 35  };
            var v1b = new ProductVariant { TenantId = t1.Id, ProductId = p1.Id, SKU = "TC-OXF-M-WHT",  Size = "M",  Color = "White",    CostPrice = 18, SellingPrice = 35  };
            var v1c = new ProductVariant { TenantId = t1.Id, ProductId = p1.Id, SKU = "TC-OXF-L-BLU",  Size = "L",  Color = "Blue",     CostPrice = 20, SellingPrice = 38  };
            var v2a = new ProductVariant { TenantId = t1.Id, ProductId = p2.Id, SKU = "TC-CHN-30-KHK", Size = "30", Color = "Khaki",    CostPrice = 22, SellingPrice = 45  };
            var v2b = new ProductVariant { TenantId = t1.Id, ProductId = p2.Id, SKU = "TC-CHN-32-NVY", Size = "32", Color = "Navy",     CostPrice = 22, SellingPrice = 45  };
            var v3a = new ProductVariant { TenantId = t1.Id, ProductId = p3.Id, SKU = "TC-BLZ-M-BGE",  Size = "M",  Color = "Beige",    CostPrice = 55, SellingPrice = 110 };
            var v3b = new ProductVariant { TenantId = t1.Id, ProductId = p3.Id, SKU = "TC-BLZ-L-CHR",  Size = "L",  Color = "Charcoal", CostPrice = 55, SellingPrice = 110 };
            context.ProductVariants.AddRange(v1a, v1b, v1c, v2a, v2b, v3a, v3b);
            await context.SaveChangesAsync();

            // Initial stock IN
            var d1 = DateTime.UtcNow.AddDays(-30);
            context.StockMovements.AddRange(
                new StockMovement { TenantId = t1.Id, ProductVariantId = v1a.Id, MovementType = "IN", Quantity = 30, MovementDate = d1, Remarks = "Initial stock" },
                new StockMovement { TenantId = t1.Id, ProductVariantId = v1b.Id, MovementType = "IN", Quantity = 40, MovementDate = d1, Remarks = "Initial stock" },
                new StockMovement { TenantId = t1.Id, ProductVariantId = v1c.Id, MovementType = "IN", Quantity = 25, MovementDate = d1, Remarks = "Initial stock" },
                new StockMovement { TenantId = t1.Id, ProductVariantId = v2a.Id, MovementType = "IN", Quantity = 20, MovementDate = d1, Remarks = "Initial stock" },
                new StockMovement { TenantId = t1.Id, ProductVariantId = v2b.Id, MovementType = "IN", Quantity = 20, MovementDate = d1, Remarks = "Initial stock" },
                new StockMovement { TenantId = t1.Id, ProductVariantId = v3a.Id, MovementType = "IN", Quantity = 10, MovementDate = d1, Remarks = "Initial stock" },
                new StockMovement { TenantId = t1.Id, ProductVariantId = v3b.Id, MovementType = "IN", Quantity = 10, MovementDate = d1, Remarks = "Initial stock" }
            );
            await context.SaveChangesAsync();

            // Sale 1 — 2× Oxford S/White + 1× Chino 30/Khaki
            var s1 = new Sale
            {
                Id = Guid.NewGuid(), TenantId = t1.Id, SaleDate = DateTime.UtcNow.AddDays(-20),
                TotalAmount = 2 * 35 + 1 * 45,
                TotalProfit = (35 - 18) * 2 + (45 - 22) * 1   // 34 + 23 = 57
            };
            context.Sales.Add(s1);
            await context.SaveChangesAsync();
            context.SaleItems.AddRange(
                new SaleItem { Id = Guid.NewGuid(), TenantId = t1.Id, SaleId = s1.Id, ProductVariantId = v1a.Id, Quantity = 2, UnitPrice = 35, CostPrice = 18, Profit = (35 - 18) * 2 },
                new SaleItem { Id = Guid.NewGuid(), TenantId = t1.Id, SaleId = s1.Id, ProductVariantId = v2a.Id, Quantity = 1, UnitPrice = 45, CostPrice = 22, Profit = (45 - 22) * 1 }
            );
            context.StockMovements.AddRange(
                new StockMovement { TenantId = t1.Id, ProductVariantId = v1a.Id, MovementType = "OUT", Quantity = 2, MovementDate = s1.SaleDate, SaleId = s1.Id, Remarks = "Sale" },
                new StockMovement { TenantId = t1.Id, ProductVariantId = v2a.Id, MovementType = "OUT", Quantity = 1, MovementDate = s1.SaleDate, SaleId = s1.Id, Remarks = "Sale" }
            );
            context.CashTransactions.Add(new CashTransaction { Id = Guid.NewGuid(), TenantId = t1.Id, TransactionDate = s1.SaleDate, Type = "IN", Category = "Sales", Amount = s1.TotalAmount, SaleId = s1.Id, ReferenceNo = "SALE-001", Remarks = "Sale income" });

            // Sale 2 — 1× Linen Blazer M/Beige
            var s2 = new Sale
            {
                Id = Guid.NewGuid(), TenantId = t1.Id, SaleDate = DateTime.UtcNow.AddDays(-10),
                TotalAmount = 110, TotalProfit = (110 - 55) * 1
            };
            context.Sales.Add(s2);
            await context.SaveChangesAsync();
            context.SaleItems.Add(new SaleItem { Id = Guid.NewGuid(), TenantId = t1.Id, SaleId = s2.Id, ProductVariantId = v3a.Id, Quantity = 1, UnitPrice = 110, CostPrice = 55, Profit = (110 - 55) * 1 });
            context.StockMovements.Add(new StockMovement { TenantId = t1.Id, ProductVariantId = v3a.Id, MovementType = "OUT", Quantity = 1, MovementDate = s2.SaleDate, SaleId = s2.Id, Remarks = "Sale" });
            context.CashTransactions.Add(new CashTransaction { Id = Guid.NewGuid(), TenantId = t1.Id, TransactionDate = s2.SaleDate, Type = "IN", Category = "Sales", Amount = s2.TotalAmount, SaleId = s2.Id, ReferenceNo = "SALE-002", Remarks = "Sale income" });

            // Sale 3 — 3× Oxford M/White
            var s3 = new Sale
            {
                Id = Guid.NewGuid(), TenantId = t1.Id, SaleDate = DateTime.UtcNow.AddDays(-3),
                TotalAmount = 3 * 35, TotalProfit = (35 - 18) * 3
            };
            context.Sales.Add(s3);
            await context.SaveChangesAsync();
            context.SaleItems.Add(new SaleItem { Id = Guid.NewGuid(), TenantId = t1.Id, SaleId = s3.Id, ProductVariantId = v1b.Id, Quantity = 3, UnitPrice = 35, CostPrice = 18, Profit = (35 - 18) * 3 });
            context.StockMovements.Add(new StockMovement { TenantId = t1.Id, ProductVariantId = v1b.Id, MovementType = "OUT", Quantity = 3, MovementDate = s3.SaleDate, SaleId = s3.Id, Remarks = "Sale" });
            context.CashTransactions.Add(new CashTransaction { Id = Guid.NewGuid(), TenantId = t1.Id, TransactionDate = s3.SaleDate, Type = "IN", Category = "Sales", Amount = s3.TotalAmount, SaleId = s3.Id, ReferenceNo = "SALE-003", Remarks = "Sale income" });

            // Expenses
            context.CashTransactions.AddRange(
                new CashTransaction { Id = Guid.NewGuid(), TenantId = t1.Id, TransactionDate = DateTime.UtcNow.AddDays(-28), Type = "OUT", Category = "Rent",      Amount = 800, ReferenceNo = "EXP-001", Remarks = "Monthly shop rent" },
                new CashTransaction { Id = Guid.NewGuid(), TenantId = t1.Id, TransactionDate = DateTime.UtcNow.AddDays(-27), Type = "OUT", Category = "Utilities", Amount = 120, ReferenceNo = "EXP-002", Remarks = "Electricity bill" },
                new CashTransaction { Id = Guid.NewGuid(), TenantId = t1.Id, TransactionDate = DateTime.UtcNow.AddDays(-15), Type = "OUT", Category = "Supplies",  Amount = 85,  ReferenceNo = "EXP-003", Remarks = "Packaging materials" }
            );

            // Textiles
            context.Textile.AddRange(
                new Textile { Id = Guid.NewGuid(), TenantId = t1.Id, Name = "Premium Cotton", PurchaseFrom = "Cotton Mills Co.", Quantity = 200, UnitPrice = 3, TotalPrice = 600, PurchaseDate = DateTime.UtcNow.AddDays(-35) },
                new Textile { Id = Guid.NewGuid(), TenantId = t1.Id, Name = "Italian Linen",  PurchaseFrom = "Euro Fabric Ltd.", Quantity = 80,  UnitPrice = 8, TotalPrice = 640, PurchaseDate = DateTime.UtcNow.AddDays(-35) }
            );

            await context.SaveChangesAsync();

            // ════════════════════════════════════════════════════════════
            //  TENANT 2 — FabricHub (Free plan)
            // ════════════════════════════════════════════════════════════
            var t2 = new Tenant
            {
                Code = "FABRICHUB", Name = "FabricHub",
                ContactEmail = "info@fabrichub.com", Country = "United States",
                CurrencyCode = "USD", IsActive = true, CreatedAt = DateTime.UtcNow
            };
            context.Tenants.Add(t2);
            await context.SaveChangesAsync();

            context.TenantSettings.Add(new TenantSetting
            {
                TenantId = t2.Id,
                ShowFinanceModule = false, ShowInventoryModule = true,
                ShowSalesModule = true, ShowReportsModule = false,
                LowStockThreshold = 3, InvoicePrefix = "FH", CreatedAt = DateTime.UtcNow
            });
            context.TenantSubscriptions.Add(new TenantSubscription
            {
                TenantId = t2.Id, PlanId = planStarter.Id,      // Starter — sales only
                StartDate = DateTime.UtcNow.AddMonths(-1), EndDate = DateTime.UtcNow.AddMonths(11),
                BillingCycle = "Monthly", Price = planStarter.PriceMonthly, IsActive = true
            });
            await context.SaveChangesAsync();

            // Users
            await userManager.CreateAsync(new ApplicationUser
            {
                UserName = "admin@fabrichub.com", Email = "admin@fabrichub.com", EmailConfirmed = true,
                FullName = "FabricHub Admin", TenantId = t2.Id,
                IsTenantAdmin = true, IsActive = true, Type = "Admin", CreatedAt = DateTime.UtcNow
            }, "Admin@1234");

            await userManager.CreateAsync(new ApplicationUser
            {
                UserName = "staff@fabrichub.com", Email = "staff@fabrichub.com", EmailConfirmed = true,
                FullName = "FabricHub Staff", TenantId = t2.Id,
                IsTenantAdmin = false, IsActive = true, Type = "Staff", CreatedAt = DateTime.UtcNow
            }, "Staff@1234");

            // Products
            var p4 = new Product { Id = Guid.NewGuid(), TenantId = t2.Id, Name = "Denim Jacket", Category = "Jackets", Brand = "StreetStyle", IsActive = true };
            var p5 = new Product { Id = Guid.NewGuid(), TenantId = t2.Id, Name = "Polo Shirt",   Category = "Shirts",  Brand = "SportLine",   IsActive = true };
            context.Products.AddRange(p4, p5);
            await context.SaveChangesAsync();

            var v4a = new ProductVariant { TenantId = t2.Id, ProductId = p4.Id, SKU = "FH-DNM-M-BLU", Size = "M", Color = "Blue",  CostPrice = 30, SellingPrice = 65 };
            var v4b = new ProductVariant { TenantId = t2.Id, ProductId = p4.Id, SKU = "FH-DNM-L-BLK", Size = "L", Color = "Black", CostPrice = 30, SellingPrice = 65 };
            var v5a = new ProductVariant { TenantId = t2.Id, ProductId = p5.Id, SKU = "FH-PLO-S-WHT", Size = "S", Color = "White", CostPrice = 12, SellingPrice = 25 };
            var v5b = new ProductVariant { TenantId = t2.Id, ProductId = p5.Id, SKU = "FH-PLO-M-RED", Size = "M", Color = "Red",   CostPrice = 12, SellingPrice = 25 };
            context.ProductVariants.AddRange(v4a, v4b, v5a, v5b);
            await context.SaveChangesAsync();

            var d2 = DateTime.UtcNow.AddDays(-20);
            context.StockMovements.AddRange(
                new StockMovement { TenantId = t2.Id, ProductVariantId = v4a.Id, MovementType = "IN", Quantity = 15, MovementDate = d2, Remarks = "Initial stock" },
                new StockMovement { TenantId = t2.Id, ProductVariantId = v4b.Id, MovementType = "IN", Quantity = 12, MovementDate = d2, Remarks = "Initial stock" },
                new StockMovement { TenantId = t2.Id, ProductVariantId = v5a.Id, MovementType = "IN", Quantity = 30, MovementDate = d2, Remarks = "Initial stock" },
                new StockMovement { TenantId = t2.Id, ProductVariantId = v5b.Id, MovementType = "IN", Quantity = 25, MovementDate = d2, Remarks = "Initial stock" }
            );
            await context.SaveChangesAsync();

            // Sale for Tenant 2 — 2× Polo S/White + 1× Denim M/Blue
            var s4 = new Sale
            {
                Id = Guid.NewGuid(), TenantId = t2.Id, SaleDate = DateTime.UtcNow.AddDays(-5),
                TotalAmount = 2 * 25 + 1 * 65,
                TotalProfit = (25 - 12) * 2 + (65 - 30) * 1   // 26 + 35 = 61
            };
            context.Sales.Add(s4);
            await context.SaveChangesAsync();
            context.SaleItems.AddRange(
                new SaleItem { Id = Guid.NewGuid(), TenantId = t2.Id, SaleId = s4.Id, ProductVariantId = v5a.Id, Quantity = 2, UnitPrice = 25, CostPrice = 12, Profit = (25 - 12) * 2 },
                new SaleItem { Id = Guid.NewGuid(), TenantId = t2.Id, SaleId = s4.Id, ProductVariantId = v4a.Id, Quantity = 1, UnitPrice = 65, CostPrice = 30, Profit = (65 - 30) * 1 }
            );
            context.StockMovements.AddRange(
                new StockMovement { TenantId = t2.Id, ProductVariantId = v5a.Id, MovementType = "OUT", Quantity = 2, MovementDate = s4.SaleDate, SaleId = s4.Id, Remarks = "Sale" },
                new StockMovement { TenantId = t2.Id, ProductVariantId = v4a.Id, MovementType = "OUT", Quantity = 1, MovementDate = s4.SaleDate, SaleId = s4.Id, Remarks = "Sale" }
            );
            context.CashTransactions.AddRange(
                new CashTransaction { Id = Guid.NewGuid(), TenantId = t2.Id, TransactionDate = s4.SaleDate, Type = "IN", Category = "Sales", Amount = s4.TotalAmount, SaleId = s4.Id, ReferenceNo = "SALE-001", Remarks = "Sale income" },
                new CashTransaction { Id = Guid.NewGuid(), TenantId = t2.Id, TransactionDate = DateTime.UtcNow.AddDays(-18), Type = "OUT", Category = "Rent",  Amount = 500,            ReferenceNo = "EXP-001",  Remarks = "Monthly shop rent" }
            );

            context.Textile.Add(new Textile { Id = Guid.NewGuid(), TenantId = t2.Id, Name = "Raw Denim", PurchaseFrom = "Denim Factory", Quantity = 150, UnitPrice = 5, TotalPrice = 750, PurchaseDate = DateTime.UtcNow.AddDays(-22) });

            await context.SaveChangesAsync();
        }
    }
}
