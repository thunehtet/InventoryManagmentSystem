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
        private static readonly string[] ManualCategories = { "Packaging Fee", "Transportation", "Other" };
        private static readonly string[] ProtectedCategories = { "Sale Income", "Sales", "Inventory Purchase", "Textile Purchase" };

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

            if (!IsAllowedManualCategory(dto.Category))
            {
                ModelState.AddModelError(nameof(dto.Category), "Select a valid manual transaction category.");
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

            TempData["SuccessMsg"]      = "Transaction recorded.";
            TempData["SuccessListUrl"]  = Url.Action("Index", "CashTransaction");
            TempData["SuccessListLabel"]= "View Transactions";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var item = await _context.CashTransactions.FindAsync(id);

            if (item == null)
                return NotFound();

            if (IsProtectedTransaction(item))
            {
                TempData["Error"] = "This transaction is managed from its source record and cannot be edited here.";
                return RedirectToAction(nameof(Index));
            }

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

            if (IsProtectedTransaction(item))
            {
                TempData["Error"] = "This transaction is managed from its source record and cannot be edited here.";
                return RedirectToAction(nameof(Index));
            }

            if (!IsAllowedManualCategory(dto.Category))
            {
                ModelState.AddModelError(nameof(dto.Category), "Select a valid manual transaction category.");
                LoadDropDowns();
                return View(dto);
            }

            item.TransactionDate = dto.TransactionDate;
            item.Type = dto.Type;
            item.Category = dto.Category;
            item.Amount = dto.Amount;
            item.ReferenceNo = dto.ReferenceNo;
            item.Remarks = dto.Remarks;

            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Transaction updated.";
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "CashTransaction");
            TempData["SuccessListLabel"]= "View Transactions";
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

            if (IsProtectedCategory(item.Category))
            {
                TempData["Error"] = "This transaction is managed from its source record and cannot be deleted here.";
                return RedirectToAction(nameof(Index));
            }

            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var item = await _context.CashTransactions.FindAsync(id);

            if (item == null)
                return NotFound();

            if (IsProtectedTransaction(item))
            {
                TempData["Error"] = "This transaction is managed from its source record and cannot be deleted here.";
                return RedirectToAction(nameof(Index));
            }

            _context.CashTransactions.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Transaction deleted.";
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "CashTransaction");
            TempData["SuccessListLabel"]= "View Transactions";
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
                new SelectListItem { Value = "Packaging Fee", Text = "Packaging Fee" },
                new SelectListItem { Value = "Transportation", Text = "Transportation" },
                new SelectListItem { Value = "Other", Text = "Other" }
            };
        }

        private static bool IsAllowedManualCategory(string? category)
        {
            return !string.IsNullOrWhiteSpace(category)
                && ManualCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsProtectedCategory(string? category)
        {
            return !string.IsNullOrWhiteSpace(category)
                && ProtectedCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsProtectedTransaction(CashTransaction item)
        {
            return item.SaleId != null || IsProtectedCategory(item.Category);
        }
    }
}
