using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Textile;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    public class TextileController : Controller
    {
        private readonly AppDbContext _context;

        public TextileController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var textiles = await _context.Textile
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

            return View(textiles);
        }

        public IActionResult Create()
        {
            return View(new CreateTextileDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTextileDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var textile = new Textile
            {
                Name = dto.Name,
                PurchaseFrom = dto.PurchaseFrom,
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
                Type = "OUT",
                Category = "Textile Purchase",
                Amount = textile.TotalPrice,
                ReferenceNo = $"Textile #{textile.Id}",
                Remarks = textile.Name
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
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

        public async Task<IActionResult> Details(int id)
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

        public async Task<IActionResult> Delete(int id)
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
        public async Task<IActionResult> DeleteConfirmed(int id)
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