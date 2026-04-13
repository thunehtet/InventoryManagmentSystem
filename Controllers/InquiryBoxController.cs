using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class InquiryBoxController : Controller
    {
        private readonly AppDbContext _db;

        public InquiryBoxController(AppDbContext db)
        {
            _db = db;
        }

        // GET /InquiryBox
        public async Task<IActionResult> Index(string? filter, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _db.ContactInquiries.AsNoTracking();

            query = filter switch
            {
                "unread" => query.Where(x => !x.IsRead),
                "read"   => query.Where(x => x.IsRead),
                _        => query
            };

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.SubmittedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            ViewBag.Filter      = filter ?? "all";
            ViewBag.UnreadCount = await _db.ContactInquiries.CountAsync(x => !x.IsRead);
            ViewBag.Pagination  = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["filter"] = filter }
            };
            return View(items);
        }

        // GET /InquiryBox/Details/id
        public async Task<IActionResult> Details(Guid id)
        {
            var item = await _db.ContactInquiries.FindAsync(id);
            if (item == null) return NotFound();

            // Mark as read when viewed
            if (!item.IsRead)
            {
                item.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return View(item);
        }

        // POST /InquiryBox/MarkRead/id
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var item = await _db.ContactInquiries.FindAsync(id);
            if (item != null)
            {
                item.IsRead = true;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST /InquiryBox/Delete/id
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _db.ContactInquiries.FindAsync(id);
            if (item != null)
            {
                _db.ContactInquiries.Remove(item);
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Inquiry deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
