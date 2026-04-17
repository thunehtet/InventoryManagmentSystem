using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Email;
using ClothInventoryApp.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class InquiryBoxController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly ITemporaryCredentialService _temporaryCredentialService;
        private readonly ILogger<InquiryBoxController> _logger;

        public InquiryBoxController(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            ITemporaryCredentialService temporaryCredentialService,
            ILogger<InquiryBoxController> logger)
        {
            _db = db;
            _userManager = userManager;
            _emailService = emailService;
            _temporaryCredentialService = temporaryCredentialService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? filter, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _db.ContactInquiries
                .AsNoTracking()
                .Include(x => x.ApprovedTenant)
                .AsQueryable();

            query = filter switch
            {
                "unread" => query.Where(x => !x.IsRead),
                "read" => query.Where(x => x.IsRead),
                "pending" => query.Where(x => x.Status == "Pending"),
                "approved" => query.Where(x => x.Status == "Approved"),
                "rejected" => query.Where(x => x.Status == "Rejected"),
                _ => query
            };

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(x => x.Status == "Pending" ? 0 : x.Status == "Approved" ? 1 : 2)
                .ThenByDescending(x => x.SubmittedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            ViewBag.Filter = filter ?? "all";
            ViewBag.UnreadCount = await _db.ContactInquiries.CountAsync(x => !x.IsRead);
            ViewBag.PendingCount = await _db.ContactInquiries.CountAsync(x => x.Status == "Pending");
            ViewBag.ApprovedCount = await _db.ContactInquiries.CountAsync(x => x.Status == "Approved");
            ViewBag.RejectedCount = await _db.ContactInquiries.CountAsync(x => x.Status == "Rejected");
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = size,
                TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["filter"] = filter }
            };
            return View(items);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var item = await _db.ContactInquiries
                .Include(x => x.ApprovedTenant)
                .Include(x => x.ReviewedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();

            if (!item.IsRead)
            {
                item.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return View(item);
        }

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(Guid id)
        {
            var inquiry = await _db.ContactInquiries.FirstOrDefaultAsync(x => x.Id == id);
            if (inquiry == null) return NotFound();

            if (!string.Equals(inquiry.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only pending inquiries can be approved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var normalizedEmail = _userManager.NormalizeEmail(inquiry.Email);
            if (await _userManager.Users.IgnoreQueryFilters().AnyAsync(u =>
                u.NormalizedEmail == normalizedEmail ||
                u.NormalizedUserName == normalizedEmail))
            {
                TempData["Error"] = "This email already has an account.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var freePlan = await _db.Plans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Code == "FREE" && p.IsActive);
            if (freePlan == null)
            {
                TempData["Error"] = "The free plan is not configured yet.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var reviewer = await _userManager.GetUserAsync(User);
            if (reviewer == null) return Unauthorized();

            var now = DateTime.UtcNow;
            var tempPassword = _temporaryCredentialService.GenerateTemporaryPassword();
            var tenantCode = await GenerateUniqueTenantCodeAsync(inquiry.BusinessName);

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var tenant = new Tenant
                {
                    Code = tenantCode,
                    Name = inquiry.BusinessName,
                    BusinessType = inquiry.BusinessType,
                    ContactEmail = inquiry.Email,
                    ContactPhone = inquiry.Phone,
                    CurrencyCode = "MMK",
                    Country = "Myanmar",
                    IsActive = true,
                    CreatedAt = now
                };

                _db.Tenants.Add(tenant);
                _db.TenantSettings.Add(new TenantSetting
                {
                    Tenant = tenant,
                    ShowFinanceModule = false,
                    ShowInventoryModule = true,
                    ShowSalesModule = true,
                    ShowReportsModule = false,
                    CreatedAt = now
                });

                _db.TenantSubscriptions.Add(new TenantSubscription
                {
                    Tenant = tenant,
                    PlanId = freePlan.Id,
                    StartDate = now.Date,
                    EndDate = now.Date.AddYears(10),
                    BillingCycle = "Monthly",
                    Price = 0,
                    IsTrial = false,
                    IsActive = true,
                    Notes = $"Created from approved inquiry {inquiry.Id}."
                });

                var user = new ApplicationUser
                {
                    UserName = inquiry.Email,
                    Email = inquiry.Email,
                    FullName = inquiry.Name,
                    Tenant = tenant,
                    IsTenantAdmin = true,
                    IsSuperAdmin = false,
                    IsActive = true,
                    Type = "Admin",
                    CreatedAt = now,
                    MustChangePassword = true
                };

                var result = await _userManager.CreateAsync(user, tempPassword);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    TempData["Error"] = string.Join(" ", result.Errors.Select(x => x.Description));
                    return RedirectToAction(nameof(Details), new { id });
                }

                inquiry.Status = "Approved";
                inquiry.ReviewedAt = now;
                inquiry.ReviewedByUserId = reviewer.Id;
                inquiry.ReviewRemarks = "Approved and converted to a free tenant account.";
                inquiry.ApprovedTenantId = tenant.Id;
                inquiry.IsRead = true;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                try
                {
                    await _emailService.SendAsync(
                        inquiry.Email,
                        "Your StockEasy workspace is ready",
                        $"""
                        <p>Hello {WebUtility.HtmlEncode(inquiry.Name)},</p>
                        <p>Your StockEasy workspace has been approved and created.</p>
                        <p><strong>Workspace:</strong> {WebUtility.HtmlEncode(inquiry.BusinessName)}</p>
                        <p><strong>Username:</strong> {WebUtility.HtmlEncode(inquiry.Email)}</p>
                        <p><strong>Temporary password:</strong> {WebUtility.HtmlEncode(tempPassword)}</p>
                        <p>Please sign in and change your password immediately.</p>
                        """,
                        HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send approval email for inquiry {InquiryId}.", inquiry.Id);
                    TempData["Warning"] = "Account created, but the approval email could not be sent.";
                }

                TempData["SuccessMsg"]      = this.LocalizeShared(
                    "Inquiry approved. Free account created for {0}. Login: {1} / {2}",
                    inquiry.BusinessName,
                    inquiry.Email,
                    tempPassword);
                TempData["SuccessType"]     = "update";
                TempData["SuccessListUrl"]  = Url.Action("Index", "InquiryBox");
                TempData["SuccessListLabel"]= "View All Inquiries";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
                throw;
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(Guid id, string? remarks)
        {
            var inquiry = await _db.ContactInquiries.FirstOrDefaultAsync(x => x.Id == id);
            if (inquiry == null) return NotFound();

            if (!string.Equals(inquiry.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only pending inquiries can be rejected.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var reviewer = await _userManager.GetUserAsync(User);
            if (reviewer == null) return Unauthorized();

            inquiry.Status = "Rejected";
            inquiry.ReviewedAt = DateTime.UtcNow;
            inquiry.ReviewedByUserId = reviewer.Id;
            inquiry.ReviewRemarks = string.IsNullOrWhiteSpace(remarks)
                ? "Rejected by SuperAdmin."
                : remarks.Trim();
            inquiry.IsRead = true;

            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = "Inquiry rejected.";
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "InquiryBox");
            TempData["SuccessListLabel"]= "View All Inquiries";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _db.ContactInquiries.FindAsync(id);
            if (item != null)
            {
                _db.ContactInquiries.Remove(item);
                await _db.SaveChangesAsync();
            }
            TempData["SuccessMsg"]      = "Inquiry deleted.";
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "InquiryBox");
            TempData["SuccessListLabel"]= "View Inquiries";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> GenerateUniqueTenantCodeAsync(string businessName)
        {
            var raw = new string((businessName ?? string.Empty)
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());

            var prefix = string.IsNullOrWhiteSpace(raw) ? "TENANT" : raw[..Math.Min(raw.Length, 8)];
            var code = prefix;
            var suffix = 1;

            while (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Code == code))
            {
                code = $"{prefix}{suffix:D2}";
                suffix++;
            }

            return code;
        }
    }
}
