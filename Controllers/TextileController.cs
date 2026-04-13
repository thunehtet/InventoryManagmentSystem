using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Textile;
using ClothInventoryApp.Filters;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    [FeatureRequired("textiles")]
    public class TextileController : TenantAwareController
    {
        public TextileController(AppDbContext context, ITenantProvider tenantProvider)
            : base(context, tenantProvider)
        {
        }
 
        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _context.Textile.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t =>
                    t.Name.Contains(search) ||
                    t.PurchaseFrom.Contains(search));

            var total = await query.CountAsync();
            var textiles = await query
                .OrderByDescending(t => t.PurchaseDate)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(t => new ViewTextileDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    PurchaseFrom = t.PurchaseFrom,
                    Quantity = t.Quantity,
                    PurchaseDate = t.PurchaseDate,
                    UnitPrice = t.UnitPrice,
                    TotalPrice = t.TotalPrice
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["search"] = search }
            };
            return View(textiles);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new CreateTextileDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateTextileDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);


            var tenantId = _tenantProvider.GetTenantId();
            var textile = new Textile
            {
                Name = dto.Name,
                PurchaseFrom = dto.PurchaseFrom,
                TenantId = tenantId,
                Quantity = dto.Quantity,
                PurchaseDate = dto.PurchaseDate,
                UnitPrice = dto.UnitPrice,
                TotalPrice = dto.Quantity * dto.UnitPrice
            };

            _context.Textile.Add(textile);
            await _context.SaveChangesAsync();

            _context.CashTransactions.Add(new CashTransaction
            {
                TransactionDate = textile.PurchaseDate,
                TenantId = tenantId,
                Type = "OUT",
                Category = "Textile Purchase",
                Amount = textile.TotalPrice,
                ReferenceNo = $"Textile #{textile.Id}",
                Remarks = textile.Name
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var textile = await _context.Textile.FindAsync(id);

            if (textile == null)
                return NotFound();

            var dto = new ViewTextileDto
            {
                Id = textile.Id,
                Name = textile.Name,
                PurchaseFrom = textile.PurchaseFrom,
                Quantity = textile.Quantity,
                PurchaseDate = textile.PurchaseDate,
                UnitPrice = textile.UnitPrice
            };

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(ViewTextileDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var textile = await _context.Textile.FindAsync(dto.Id);

            if (textile == null)
                return NotFound();

            textile.Name = dto.Name;
            textile.PurchaseFrom = dto.PurchaseFrom;
            textile.Quantity = dto.Quantity;
            textile.PurchaseDate = dto.PurchaseDate;
            textile.UnitPrice = dto.UnitPrice;
            textile.TotalPrice = dto.Quantity * dto.UnitPrice;

            _context.Textile.Update(textile);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var textile = await _context.Textile
                .Select(t => new ViewTextileDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    PurchaseFrom = t.PurchaseFrom,
                    Quantity = t.Quantity,
                    PurchaseDate = t.PurchaseDate,
                    UnitPrice = t.UnitPrice,
                    TotalPrice = t.TotalPrice
                })
                .FirstOrDefaultAsync(t => t.Id == id);

            if (textile == null)
                return NotFound();

            return View(textile);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var textile = await _context.Textile
                .Select(t => new ViewTextileDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    PurchaseFrom = t.PurchaseFrom,
                    Quantity = t.Quantity,
                    PurchaseDate = t.PurchaseDate,
                    UnitPrice = t.UnitPrice,
                    TotalPrice = t.TotalPrice
                })
                .FirstOrDefaultAsync(t => t.Id == id);

            if (textile == null)
                return NotFound();

            return View(textile);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var textile = await _context.Textile.FindAsync(id);

            if (textile == null)
                return NotFound();

            _context.Textile.Remove(textile);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}