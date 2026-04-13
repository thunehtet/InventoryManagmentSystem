using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.CashTransaction;
using ClothInventoryApp.Filters;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    [FeatureRequired("finance")]
    public class CashTransactionController : TenantAwareController
    {
        public CashTransactionController(AppDbContext context, ITenantProvider tenantProvider)
            : base(context, tenantProvider)
        {
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _context.CashTransactions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x =>
                    x.Category.Contains(search) ||
                    x.Type.Contains(search) ||
                    (x.ReferenceNo != null && x.ReferenceNo.Contains(search)) ||
                    (x.Remarks != null && x.Remarks.Contains(search)));

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.TransactionDate)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(x => new ViewCashTransactionDto
                {
                    Id = x.Id,
                    TransactionDate = x.TransactionDate,
                    Type = x.Type,
                    Category = x.Category,
                    Amount = x.Amount,
                    ReferenceNo = x.ReferenceNo,
                    Remarks = x.Remarks
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["search"] = search }
            };
            return View(items);
        }

        public IActionResult Create()
        {
            LoadDropDowns();
            return View(new CreateCashTransactionDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCashTransactionDto dto)
        {
            if (!ModelState.IsValid)
            {
                LoadDropDowns();
                return View(dto);
            }
            var tenantId = _tenantProvider.GetTenantId();
            var item = new CashTransaction
            {
                TransactionDate = dto.TransactionDate,
                Type = dto.Type,
                TenantId = tenantId,
                Category = dto.Category,
                Amount = dto.Amount,
                ReferenceNo = dto.ReferenceNo,
                Remarks = dto.Remarks
            };

            _context.CashTransactions.Add(item);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var item = await _context.CashTransactions.FindAsync(id);

            if (item == null)
                return NotFound();

            var dto = new CreateCashTransactionDto
            {
                Id = item.Id,
                TransactionDate = item.TransactionDate,
                Type = item.Type,
                Category = item.Category,
                Amount = item.Amount,
                ReferenceNo = item.ReferenceNo,
                Remarks = item.Remarks
            };

            LoadDropDowns();
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CreateCashTransactionDto dto)
        {
            if (!ModelState.IsValid)
            {
                LoadDropDowns();
                return View(dto);
            }

            var item = await _context.CashTransactions.FindAsync(dto.Id);

            if (item == null)
                return NotFound();

            item.TransactionDate = dto.TransactionDate;
            item.Type = dto.Type;
            item.Category = dto.Category;
            item.Amount = dto.Amount;
            item.ReferenceNo = dto.ReferenceNo;
            item.Remarks = dto.Remarks;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var item = await _context.CashTransactions
                .Where(x => x.Id == id)
                .Select(x => new ViewCashTransactionDto
                {
                    Id = x.Id,
                    TransactionDate = x.TransactionDate,
                    Type = x.Type,
                    Category = x.Category,
                    Amount = x.Amount,
                    ReferenceNo = x.ReferenceNo,
                    Remarks = x.Remarks
                })
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound();

            return View(item);
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.CashTransactions
                .Where(x => x.Id == id)
                .Select(x => new ViewCashTransactionDto
                {
                    Id = x.Id,
                    TransactionDate = x.TransactionDate,
                    Type = x.Type,
                    Category = x.Category,
                    Amount = x.Amount,
                    ReferenceNo = x.ReferenceNo,
                    Remarks = x.Remarks
                })
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound();

            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var item = await _context.CashTransactions.FindAsync(id);

            if (item == null)
                return NotFound();

            _context.CashTransactions.Remove(item);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private void LoadDropDowns()
        {
            ViewBag.Types = new List<SelectListItem>
            {
                new SelectListItem { Value = "IN", Text = "IN" },
                new SelectListItem { Value = "OUT", Text = "OUT" }
            };

            ViewBag.Categories = new List<SelectListItem>
            {
                new SelectListItem { Value = "Sale Income", Text = "Sale Income" },
                new SelectListItem { Value = "Textile Purchase", Text = "Textile Purchase" },
                new SelectListItem { Value = "Tailor Fee", Text = "Tailor Fee" },
                new SelectListItem { Value = "Transport", Text = "Transport" },
                new SelectListItem { Value = "Packaging", Text = "Packaging" },
                new SelectListItem { Value = "Utilities", Text = "Utilities" },
                new SelectListItem { Value = "Living Expense", Text = "Living Expense" },
                new SelectListItem { Value = "Other", Text = "Other" }
            };
        }
    }
}