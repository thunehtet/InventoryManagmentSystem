using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Customer;
using ClothInventoryApp.Filters;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    [FeatureRequired("customers")]
    public class CustomerController : TenantAwareController
    {
        public CustomerController(AppDbContext context, ITenantProvider tenantProvider)
            : base(context, tenantProvider) { }

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _context.Customers.AsNoTracking()
                .Include(c => c.Sales)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(s) ||
                    (c.Phone != null && c.Phone.Contains(s)) ||
                    (c.FacebookAccount != null && c.FacebookAccount.ToLower().Contains(s)) ||
                    (c.Email != null && c.Email.ToLower().Contains(s)));
            }

            var total = await query.CountAsync();
            var customers = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(c => new ViewCustomerDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    Email = c.Email,
                    FacebookAccount = c.FacebookAccount,
                    Address = c.Address,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    TotalSales = c.Sales.Count,
                    TotalRevenue = c.Sales.Sum(s => s.TotalAmount)
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["search"] = search }
            };
            return View(customers);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var customer = await _context.Customers
                .Include(c => c.Sales)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null) return NotFound();

            var dto = new ViewCustomerDto
            {
                Id = customer.Id,
                Name = customer.Name,
                Phone = customer.Phone,
                Email = customer.Email,
                FacebookAccount = customer.FacebookAccount,
                Address = customer.Address,
                Notes = customer.Notes,
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt,
                TotalSales = customer.Sales.Count,
                TotalRevenue = customer.Sales.Sum(s => s.TotalAmount)
            };

            ViewBag.Sales = customer.Sales
                .OrderByDescending(s => s.SaleDate)
                .Select(s => new { s.Id, s.SaleDate, s.TotalAmount, s.Discount, s.TotalProfit })
                .ToList();

            return View(dto);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View(new CreateCustomerDto());

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateCustomerDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var tenantId = _tenantProvider.GetTenantId();
            _context.Customers.Add(new Customer
            {
                TenantId = tenantId,
                Name = dto.Name,
                Phone = dto.Phone,
                Email = dto.Email,
                FacebookAccount = dto.FacebookAccount,
                Address = dto.Address,
                Notes = dto.Notes,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Customer '{dto.Name}' added.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var c = await _context.Customers.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            return View(new EditCustomerDto
            {
                Id = c.Id, Name = c.Name, Phone = c.Phone,
                Email = c.Email, FacebookAccount = c.FacebookAccount,
                Address = c.Address, Notes = c.Notes, IsActive = c.IsActive
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(EditCustomerDto dto)
        {
            if (!ModelState.IsValid) return View(dto);
            var c = await _context.Customers.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (c == null) return NotFound();

            c.Name = dto.Name; c.Phone = dto.Phone; c.Email = dto.Email;
            c.FacebookAccount = dto.FacebookAccount; c.Address = dto.Address;
            c.Notes = dto.Notes; c.IsActive = dto.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Customer '{c.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var c = await _context.Customers
                .Include(x => x.Sales)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            return View(c);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var c = await _context.Customers
                .Include(x => x.Sales)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            // Nullify customer link on existing sales (FK is SET NULL)
            foreach (var sale in c.Sales)
                sale.CustomerId = null;

            _context.Customers.Remove(c);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Customer '{c.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
