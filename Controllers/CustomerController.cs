using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Customer;
using ClothInventoryApp.Filters;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Subscription;
using ClothInventoryApp.Services.Tenant;
using ClothInventoryApp.Services.Usage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    [FeatureRequired("customers")]
    public class CustomerController : TenantAwareController
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IUsageTrackingService _usageTrackingService;

        public CustomerController(
            AppDbContext context,
            ITenantProvider tenantProvider,
            ISubscriptionService subscriptionService,
            IUsageTrackingService usageTrackingService)
            : base(context, tenantProvider)
        {
            _subscriptionService = subscriptionService;
            _usageTrackingService = usageTrackingService;
        }

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

            ViewBag.InviteLinks = await _context.CustomerInviteLinks
                .AsNoTracking()
                .Include(x => x.Customer)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new CustomerInviteLinkDto
                {
                    Id = x.Id,
                    Token = x.Token,
                    Url = Url.Action(
                        "RegisterByInvite",
                        "Public",
                        new { token = x.Token },
                        Request.Scheme)!,
                    ExpiresAt = x.ExpiresAt,
                    IsActive = x.IsActive,
                    IsExpired = x.ExpiresAt <= DateTime.UtcNow,
                    IsUsed = x.UsedAt != null,
                    CreatedAt = x.CreatedAt,
                    UsedAt = x.UsedAt,
                    CustomerName = x.Customer != null ? x.Customer.Name : null
                })
                .ToListAsync();

            var tenantId = _tenantProvider.GetTenantId();
            var (inviteUsed, inviteMax) = await _subscriptionService.GetFeatureUsageAsync(tenantId, FeatureUsageKeys.CustomerInvite);
            ViewBag.InviteUsed = inviteUsed;
            ViewBag.InviteMax  = inviteMax;

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
            var customer = new Customer
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
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            TempData["SuccessMsg"]      = this.LocalizeShared("Customer '{0}' added.", dto.Name);
            TempData["SuccessListUrl"]  = Url.Action("Index", "Customer");
            TempData["SuccessListLabel"]= this.LocalizeShared("View Customers");
            await _usageTrackingService.TrackActionAsync(tenantId, "customers", "create", "Customer", customer.Id.ToString(), $"Created customer {dto.Name}.", cancellationToken: HttpContext.RequestAborted);
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
            TempData["SuccessMsg"]      = this.LocalizeShared("Customer '{0}' updated.", c.Name);
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Customer");
            TempData["SuccessListLabel"]= this.LocalizeShared("View Customers");
            await _usageTrackingService.TrackActionAsync(c.TenantId, "customers", "update", "Customer", c.Id.ToString(), $"Updated customer {c.Name}.", cancellationToken: HttpContext.RequestAborted);
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
            TempData["SuccessMsg"]      = this.LocalizeShared("Customer '{0}' deleted.", c.Name);
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Customer");
            TempData["SuccessListLabel"]= this.LocalizeShared("View Customers");
            await _usageTrackingService.TrackActionAsync(c.TenantId, "customers", "delete", "Customer", c.Id.ToString(), $"Deleted customer {c.Name}.", cancellationToken: HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateInviteLink()
        {
            var tenantId = _tenantProvider.GetTenantId();

            if (!await _subscriptionService.CanUseFeatureAsync(tenantId, FeatureUsageKeys.CustomerInvite))
            {
                var (used, max) = await _subscriptionService.GetFeatureUsageAsync(tenantId, FeatureUsageKeys.CustomerInvite);
                TempData["LimitFeature"] = FeatureUsageKeys.CustomerInvite;
                TempData["LimitUsed"]    = used;
                TempData["LimitMax"]     = max;
                return RedirectToAction(nameof(Index));
            }

            await _subscriptionService.IncrementFeatureUsageAsync(tenantId, FeatureUsageKeys.CustomerInvite);

            var link = new CustomerInviteLink
            {
                TenantId = tenantId,
                Token = GenerateToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.CustomerInviteLinks.Add(link);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = this.LocalizeShared("Customer invite link created.");
            TempData["SuccessListUrl"]  = Url.Action("Index", "Customer");
            TempData["SuccessListLabel"]= this.LocalizeShared("View Customers");
            await _usageTrackingService.TrackActionAsync(
                tenantId,
                "customers",
                "create-invite",
                "CustomerInviteLink",
                link.Id.ToString(),
                "Created customer invite link.",
                cancellationToken: HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RevokeInviteLink(Guid id)
        {
            var link = await _context.CustomerInviteLinks.FirstOrDefaultAsync(x => x.Id == id);
            if (link == null)
                return NotFound();

            link.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = this.LocalizeShared("Customer invite link revoked.");
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Customer");
            TempData["SuccessListLabel"]= this.LocalizeShared("View Customers");
            await _usageTrackingService.TrackActionAsync(
                link.TenantId,
                "customers",
                "revoke-invite",
                "CustomerInviteLink",
                link.Id.ToString(),
                "Revoked customer invite link.",
                cancellationToken: HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        private static string GenerateToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        }
    }
}
