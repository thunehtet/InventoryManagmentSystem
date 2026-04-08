using ClothInventoryApp.Data;
using ClothInventoryApp.Dto;
using Microsoft.AspNetCore.Mvc;
using ClothInventoryApp.Models;

namespace ClothInventoryApp.Controllers
{
    public class StockController : Controller
    {
        private readonly AppDbContext _context;
        public StockController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(StockInViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var movement = new StockMovement
            {
                ProductVariantId = vm.ProductVariantId,
                Quantity = vm.Quantity,
                MovementType = "IN",
                MovementDate = DateTime.UtcNow,
                Remarks = vm.Remarks
            };

            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

            return RedirectToAction("Inventory");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(StockInViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var movement = new StockMovement
            {
                ProductVariantId = vm.ProductVariantId,
                Quantity = vm.Quantity,
                MovementType = "OUT",
                MovementDate = DateTime.UtcNow,
                Remarks = vm.Remarks
            };

            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

            return RedirectToAction("Inventory");
        }
    }
}
