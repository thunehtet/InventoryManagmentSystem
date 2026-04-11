using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Sale;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    public class SalesController : Controller
    {
        private readonly AppDbContext _context;

        private readonly ITenantProvider _tenantProvider;

        public SalesController(AppDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }
       

        public async Task<IActionResult> Index()
        {
            var sales = await _context.Sales
                .Select(s => new ViewSaleDto
                {
                    Id = s.Id,
                    SaleDate = s.SaleDate,
                    TotalAmount = s.TotalAmount
                })
                .ToListAsync();

            return View(sales);
        }

        public async Task<IActionResult> Create()
        {
            await LoadVariantDropDown();
            return View(new CreateSaleDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSaleDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadVariantDropDown();
                return View(dto);
            }

            var tenantId = _tenantProvider.GetTenantId();
            dto.Items = dto.Items
                .Where(x =>  x.Quantity > 0)
                .ToList();

            if (!dto.Items.Any())
            {
                ModelState.AddModelError("", "Please add at least one sale item.");
                await LoadVariantDropDown();
                return View(dto);
            }

            foreach (var item in dto.Items)
            {
                var currentStock = await _context.StockMovements
                    .Where(x => x.ProductVariantId == item.ProductVariantId)
                    .SumAsync(x => x.MovementType == "IN" ? x.Quantity :
                                   x.MovementType == "OUT" ? -x.Quantity :
                                   0);

                if (currentStock < item.Quantity)
                {
                    ModelState.AddModelError("", $"Not enough stock for variant ID {item.ProductVariantId}.");
                    await LoadVariantDropDown();
                    return View(dto);
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var sale = new Sale
                {
                    SaleDate = dto.SaleDate,
                    Items = dto.Items.Select(i => new SaleItem
                    {
                        ProductVariantId = i.ProductVariantId,
                        TenantId = tenantId,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        CostPrice = i.CostPrice
                    }).ToList()
                };

                sale.TotalAmount = sale.Items.Sum(x => x.Quantity * x.UnitPrice);
                

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in sale.Items)
                {
                    _context.StockMovements.Add(new StockMovement
                    {
                        ProductVariantId = item.ProductVariantId,
                        TenantId = tenantId,
                        Quantity = item.Quantity,
                        MovementType = "OUT",
                        MovementDate = DateTime.UtcNow,
                        Remarks = $"Sale #{sale.Id}"
                    });
                }

                _context.CashTransactions.Add(new CashTransaction
                {
                    TransactionDate = sale.SaleDate,
                    TenantId = tenantId,
                    Type = "IN",
                    Category = "Sale Income",
                    Amount = sale.TotalAmount,
                    ReferenceNo = $"Sale #{sale.Id}",
                    Remarks = "Auto generated from sale"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "An error occurred while saving the sale.");
                await LoadVariantDropDown();
                return View(dto);
            }
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var sale = await _context.Sales
                .Include(s => s.Items)
                .ThenInclude(i => i.ProductVariant)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return NotFound();

            var dto = new ViewSaleDto
            {
                Id = sale.Id,
                SaleDate = sale.SaleDate,
                TotalAmount = sale.TotalAmount,
                Items = sale.Items.Select(i => new ViewSaleItemDto
                {
                    Id = i.Id,
                    ProductVariantId = i.ProductVariantId,
                    ProductVariantName = i.ProductVariant.SKU + " / " + i.ProductVariant.Color + " / " + i.ProductVariant.Size,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    CostPrice = i.CostPrice,
                    LineTotal = i.Quantity * i.UnitPrice
                }).ToList()
            };

            return View(dto);
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var sale = await _context.Sales
                .Include(s => s.Items)
                .ThenInclude(i => i.ProductVariant)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return NotFound();

            var dto = new ViewSaleDto
            {
                Id = sale.Id,
                SaleDate = sale.SaleDate,
                TotalAmount = sale.TotalAmount,
                Items = sale.Items.Select(i => new ViewSaleItemDto
                {
                    Id = i.Id,
                    ProductVariantId = i.ProductVariantId,
                    ProductVariantName = i.ProductVariant.SKU + " / " + i.ProductVariant.Color + " / " + i.ProductVariant.Size,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    CostPrice = i.CostPrice,
                    LineTotal = i.Quantity * i.UnitPrice
                }).ToList()
            };

            return View(dto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return NotFound();

            _context.SaleItems.RemoveRange(sale.Items);
            _context.Sales.Remove(sale);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadVariantDropDown()
        {
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .OrderBy(v => v.SKU)
                .Select(v => new
                {
                    v.Id,
                    Text = v.Product.Name + " - " + v.SKU + " - " + v.Color + " - " + v.Size
                })
                .ToListAsync();

            ViewBag.ProductVariants = new SelectList(variants, "Id", "Text");
        }
    }
}