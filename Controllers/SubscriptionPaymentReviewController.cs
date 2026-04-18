using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.SuperAdmin;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Email;
using ClothInventoryApp.Services.Files;
using ClothInventoryApp.Services.Subscription;
using ClothInventoryApp.Services.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SubscriptionPaymentReviewController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly IEmailService _emailService;
        private readonly ITelegramService _telegramService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionPaymentReviewController> _logger;

        public SubscriptionPaymentReviewController(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            IEmailService emailService,
            ITelegramService telegramService,
            ISubscriptionService subscriptionService,
            ILogger<SubscriptionPaymentReviewController> logger)
        {
            _db = db;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _emailService = emailService;
            _telegramService = telegramService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? status = "Pending", string? search = null, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _db.SubscriptionPaymentRequests
                .AsNoTracking()
                .Include(r => r.Tenant)
                .Include(r => r.RequestedByUser)
                .Include(r => r.PaymentProofFile)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();
                query = query.Where(r =>
                    r.Tenant.Name.ToLower().Contains(keyword) ||
                    r.PlanNameSnapshot.ToLower().Contains(keyword) ||
                    r.Last6TransactionId.ToLower().Contains(keyword) ||
                    r.RequestedByUser.FullName.ToLower().Contains(keyword));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(r => r.Status == "Pending" ? 0 : 1)
                .ThenByDescending(r => r.SubmittedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(r => new SubscriptionPaymentRequestReviewDto
                {
                    Id = r.Id,
                    TenantId = r.TenantId,
                    TenantName = r.Tenant.Name,
                    RequestedByName = r.RequestedByUser.FullName,
                    RequestedByEmail = r.RequestedByUser.Email ?? string.Empty,
                    PlanId = r.PlanId,
                    PlanName = r.PlanNameSnapshot,
                    BillingCycle = r.BillingCycle,
                    ExpectedPrice = r.ExpectedPrice,
                    Last6TransactionId = r.Last6TransactionId,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    PaymentProofUrl = "/" + r.PaymentProofFile.RelativePath,
                    PaymentProofName = r.PaymentProofFile.OriginalFileName,
                    ReviewRemarks = r.ReviewRemarks,
                    ReviewedAt = r.ReviewedAt
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = size,
                TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["status"] = status, ["search"] = search }
            };

            return View(items);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var request = await LoadRequestAsync(id);
            if (request == null) return NotFound();
            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(Guid id, string? remarks)
        {
            var request = await _db.SubscriptionPaymentRequests
                .Include(r => r.Tenant)
                .Include(r => r.Plan)
                .Include(r => r.PaymentProofFile)
                .Include(r => r.RequestedByUser)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only pending requests can be approved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var reviewer = await _userManager.GetUserAsync(User);
            if (reviewer == null) return Unauthorized();

            var approvalTime = DateTime.UtcNow;
            var startDate = approvalTime.Date;
            var endDate = request.BillingCycle == "Yearly"
                ? startDate.AddYears(1).AddDays(-1)
                : startDate.AddMonths(1).AddDays(-1);

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
            var activeSubscriptions = await _db.TenantSubscriptions
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == request.TenantId && s.IsActive)
                .Include(s => s.Plan)
                .Include(s => s.Tenant)
                .ToListAsync();

            foreach (var active in activeSubscriptions)
            {
                _db.PastSubscriptions.Add(
                    SubscriptionExpiryService.BuildArchiveRecord(
                        active,
                        approvalTime,
                        "Replaced",
                        $"Replaced by approved payment request {request.Id} on {approvalTime:yyyy-MM-dd}."));
                active.IsActive = false;
                active.UpdatedAt = approvalTime;
            }

            var subscription = new TenantSubscription
            {
                TenantId = request.TenantId,
                PlanId = request.PlanId,
                StartDate = startDate,
                EndDate = endDate,
                BillingCycle = request.BillingCycle,
                Price = request.ExpectedPrice,
                IsTrial = false,
                IsActive = true,
                Notes = $"Approved from payment request {request.Id}. Transaction last 6: {request.Last6TransactionId}.",
                CreatedAt = approvalTime
            };

            _db.TenantSubscriptions.Add(subscription);

            request.Status = "Approved";
            request.ReviewedAt = approvalTime;
            request.ReviewedByUserId = reviewer.Id;
            request.ReviewRemarks = string.IsNullOrWhiteSpace(remarks) ? "Approved by SuperAdmin." : remarks.Trim();
            request.ApprovedSubscriptionId = subscription.Id;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
                throw;
            }

            var approvedPlan = await _db.Plans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == request.PlanId);
            if (string.Equals(approvedPlan?.Code, "FREE", StringComparison.OrdinalIgnoreCase))
                await _subscriptionService.ResetMonthlyFeatureUsageAsync(request.TenantId);

            try
            {
                await _emailService.SendAsync(
                    request.RequestedByUser?.Email ?? string.Empty,
                    "Subscription payment approved",
                    $"""
                    <p>Hello {WebUtility.HtmlEncode(request.RequestedByUser?.FullName ?? "there")},</p>
                    <p>Your subscription request has been approved.</p>
                    <p><strong>Plan:</strong> {WebUtility.HtmlEncode(request.PlanNameSnapshot)}</p>
                    <p><strong>Billing cycle:</strong> {WebUtility.HtmlEncode(request.BillingCycle)}</p>
                    <p>Your subscription is now active.</p>
                    """,
                    HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send approval email for payment request {RequestId}.", request.Id);
                TempData["Warning"] = "Subscription activated, but the approval email could not be sent.";
            }

            if (!string.IsNullOrWhiteSpace(request.RequestedByUser?.TelegramChatId))
            {
                var msg = $"✅ StockEasy\n\nHello {request.RequestedByUser.FullName},\n\n" +
                          $"Your {request.PlanNameSnapshot} subscription has been approved and is now active.";
                await _telegramService.SendMessageAsync(request.RequestedByUser.TelegramChatId, msg, HttpContext.RequestAborted);
            }

            TempData["SuccessMsg"]      = "Payment request approved and subscription activated.";
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "SubscriptionPaymentReview");
            TempData["SuccessListLabel"]= "View All Requests";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(Guid id, string? remarks)
        {
            var request = await _db.SubscriptionPaymentRequests
                .Include(r => r.RequestedByUser)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only pending requests can be rejected.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var reviewer = await _userManager.GetUserAsync(User);
            if (reviewer == null) return Unauthorized();

            request.Status = "Rejected";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByUserId = reviewer.Id;
            request.ReviewRemarks = string.IsNullOrWhiteSpace(remarks) ? "Rejected by SuperAdmin." : remarks.Trim();
            await _db.SaveChangesAsync();

            try
            {
                await _emailService.SendAsync(
                    request.RequestedByUser?.Email ?? string.Empty,
                    "Subscription payment rejected",
                    $"""
                    <p>Hello {WebUtility.HtmlEncode(request.RequestedByUser?.FullName ?? "there")},</p>
                    <p>Your subscription payment request for <strong>{WebUtility.HtmlEncode(request.PlanNameSnapshot)}</strong> was rejected.</p>
                    <p>Remarks: {WebUtility.HtmlEncode(request.ReviewRemarks ?? "Rejected by SuperAdmin.")}</p>
                    """,
                    HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send rejection email for payment request {RequestId}.", request.Id);
                TempData["Warning"] = "Request rejected, but the rejection email could not be sent.";
            }

            if (!string.IsNullOrWhiteSpace(request.RequestedByUser?.TelegramChatId))
            {
                var msg = $"❌ StockEasy\n\nHello {request.RequestedByUser.FullName},\n\n" +
                          $"Your payment request for {request.PlanNameSnapshot} was rejected.\n\n" +
                          $"Reason: {request.ReviewRemarks}";
                await _telegramService.SendMessageAsync(request.RequestedByUser.TelegramChatId, msg, HttpContext.RequestAborted);
            }

            TempData["SuccessMsg"]      = "Payment request rejected.";
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "SubscriptionPaymentReview");
            TempData["SuccessListLabel"]= "View All Requests";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<SubscriptionPaymentRequestReviewDto?> LoadRequestAsync(Guid id)
        {
            var request = await _db.SubscriptionPaymentRequests
                .AsNoTracking()
                .Include(r => r.Tenant)
                .Include(r => r.RequestedByUser)
                .Include(r => r.PaymentProofFile)
                .Include(r => r.ReviewedByUser)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return null;

            return new SubscriptionPaymentRequestReviewDto
            {
                Id = request.Id,
                TenantId = request.TenantId,
                TenantName = request.Tenant.Name,
                RequestedByName = request.RequestedByUser.FullName,
                RequestedByEmail = request.RequestedByUser.Email ?? string.Empty,
                PlanId = request.PlanId,
                PlanName = request.PlanNameSnapshot,
                BillingCycle = request.BillingCycle,
                ExpectedPrice = request.ExpectedPrice,
                Last6TransactionId = request.Last6TransactionId,
                Status = request.Status,
                SubmittedAt = request.SubmittedAt,
                PaymentProofUrl = _fileStorageService.GetPublicUrl(request.PaymentProofFile),
                PaymentProofName = request.PaymentProofFile.OriginalFileName,
                ReviewRemarks = request.ReviewRemarks,
                ReviewedAt = request.ReviewedAt,
                ReviewedByName = request.ReviewedByUser?.FullName
            };
        }
    }
}
