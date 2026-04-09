using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new DashboardViewModel();

            // Total Products
            vm.TotalProducts = await _context.Products.CountAsync();

            // Total Variants
            vm.TotalVariants = await _context.ProductVariants.CountAsync();

            // Low Stock (example: stock < 10)
            var stockData = await _context.ProductVariants
                .Select(v => new
                {
                    v.Id,
                    Stock =
                        v.StockMovements.Where(m => m.MovementType == "IN").Sum(m => (int?)m.Quantity) ?? 0
                        - v.StockMovements.Where(m => m.MovementType == "OUT").Sum(m => (int?)m.Quantity) ?? 0
                })
                .ToListAsync();

            vm.LowStockCount = stockData.Count(x => x.Stock < 10);

            // Recent Activity (latest stock movements)
            vm.RecentActivities = await _context.StockMovements
                .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Product)
                .OrderByDescending(s => s.MovementDate)
                .Take(5)
                .Select(s => new RecentActivityDto
                {
                    SKU = s.ProductVariant.SKU,
                    ProductName = s.ProductVariant.Product.Name,
                    Action = s.MovementType == "IN" ? "Stock In" : "Stock Out",
                    Quantity = s.MovementType == "IN" ? s.Quantity : -s.Quantity,
                    Date = s.MovementDate,
                    Status = "Completed"
                })
                .ToListAsync();

            return View(vm);
        }
    }
}