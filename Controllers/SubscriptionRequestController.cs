using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.SubscriptionRequest;
using ClothInventoryApp.Models;
using ClothInventoryApp.Options;
using ClothInventoryApp.Services.Files;
using ClothInventoryApp.Services.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SubscriptionRequestController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly ITelegramService _telegramService;
        private readonly SubscriptionPaymentSettings _paymentSettings;
        private const string DefaultQrImageUrl = "/paymentqr/mykpayqr.jpeg";

        public SubscriptionRequestController(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            ITelegramService telegramService,
            IOptions<SubscriptionPaymentSettings> paymentSettings)
        {
            _db = db;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _telegramService = telegramService;
            _paymentSettings = paymentSettings.Value;
        }

        public async Task<IActionResult> Plans()
        {
            var user = await RequireCurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");

            var tenantId = user.TenantId;
            var today = DateTime.UtcNow;
            var currentPlanId = await _db.TenantSubscriptions
                .Where(s => s.TenantId == tenantId && s.IsActive && s.StartDate <= today && s.EndDate >= today)
                .OrderByDescending(s => s.EndDate)
                .Select(s => (Guid?)s.PlanId)
                .FirstOrDefaultAsync();

            var pendingPlanIds = await _db.SubscriptionPaymentRequests
                .Where(r => r.TenantId == tenantId && r.Status == "Pending")
                .Select(r => r.PlanId)
                .Distinct()
                .ToListAsync();

            var plans = await _db.Plans
                .IgnoreQueryFilters()
                .Where(p => p.IsActive && p.Code != "FREE")
                .OrderBy(p => p.PriceMonthly)
                .Select(p => new SubscriptionRequestPlanCardDto
                {
                    PlanId = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    Description = p.Description,
                    PriceMonthly = p.PriceMonthly,
                    PriceYearly = p.PriceYearly,
                    MaxUsers = p.MaxUsers,
                    MaxProducts = p.MaxProducts,
                    MaxVariants = p.MaxVariants,
                    IsCurrent = currentPlanId == p.Id,
                    HasPendingRequest = pendingPlanIds.Contains(p.Id)
                })
                .ToListAsync();

            return View(plans);
        }

        public async Task<IActionResult> Payment(Guid planId, string billingCycle = "Monthly")
        {
            var user = await RequireCurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");

            var plan = await _db.Plans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == planId && p.IsActive);
            if (plan == null) return NotFound();
            if (string.Equals(plan.Code, "FREE", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "The free plan is assigned after signup approval and does not require payment.";
                return RedirectToAction(nameof(Plans));
            }

            billingCycle = NormalizeBillingCycle(billingCycle);
            if (billingCycle == "Yearly" && !plan.PriceYearly.HasValue)
            {
                TempData["Error"] = "Yearly billing is not available for the selected plan.";
                return RedirectToAction(nameof(Plans));
            }

            var vm = new SubscriptionPaymentPageDto
            {
                PlanId = plan.Id,
                PlanName = plan.Name,
                PlanCode = plan.Code,
                BillingCycle = billingCycle,
                Price = billingCycle == "Yearly" ? plan.PriceYearly!.Value : plan.PriceMonthly,
                CurrencyCode = (await _db.Tenants.Where(t => t.Id == user.TenantId).Select(t => t.CurrencyCode).FirstOrDefaultAsync()) ?? string.Empty,
                WalletDisplayName = _paymentSettings.WalletDisplayName,
                WalletAccountName = _paymentSettings.WalletAccountName,
                WalletAccountNo = _paymentSettings.WalletAccountNo,
                QrImageUrl = ResolveQrImageUrl(),
                Instructions = _paymentSettings.Instructions,
                Form = new CreateSubscriptionPaymentRequestDto
                {
                    PlanId = plan.Id,
                    BillingCycle = billingCycle
                }
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Payment(CreateSubscriptionPaymentRequestDto dto, CancellationToken cancellationToken)
        {
            var user = await RequireCurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");

            var plan = await _db.Plans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == dto.PlanId && p.IsActive, cancellationToken);
            if (plan == null) return NotFound();
            if (string.Equals(plan.Code, "FREE", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "The free plan does not require a payment request.";
                return RedirectToAction(nameof(Plans));
            }

            dto.BillingCycle = NormalizeBillingCycle(dto.BillingCycle);
            if (dto.BillingCycle == "Yearly" && !plan.PriceYearly.HasValue)
                ModelState.AddModelError(nameof(dto.BillingCycle), "Yearly billing is not available for the selected plan.");

            if (!string.IsNullOrWhiteSpace(dto.Last6TransactionId))
                dto.Last6TransactionId = dto.Last6TransactionId.Trim();

            if (!ModelState.IsValid)
            {
                return View(await BuildPaymentPageDtoAsync(user.TenantId, plan, dto));
            }

            var alreadyPending = await _db.SubscriptionPaymentRequests.AnyAsync(
                r => r.TenantId == user.TenantId
                    && r.PlanId == plan.Id
                    && r.BillingCycle == dto.BillingCycle
                    && r.Status == "Pending",
                cancellationToken);

            if (alreadyPending)
            {
                ModelState.AddModelError(string.Empty, "A pending payment request already exists for this plan and billing cycle.");
                return View(await BuildPaymentPageDtoAsync(user.TenantId, plan, dto));
            }

            UploadedFile paymentProof;
            try
            {
                paymentProof = await _fileStorageService.SaveImageAsync(
                    dto.PaymentProof!,
                    UploadCategories.SubscriptionPaymentProof,
                    user.Id,
                    user.TenantId,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(dto.PaymentProof), ex.Message);
                return View(await BuildPaymentPageDtoAsync(user.TenantId, plan, dto));
            }

            var price = dto.BillingCycle == "Yearly" ? plan.PriceYearly!.Value : plan.PriceMonthly;
            _db.SubscriptionPaymentRequests.Add(new SubscriptionPaymentRequest
            {
                TenantId = user.TenantId,
                RequestedByUserId = user.Id,
                PlanId = plan.Id,
                PlanNameSnapshot = plan.Name,
                BillingCycle = dto.BillingCycle,
                ExpectedPrice = price,
                Last6TransactionId = dto.Last6TransactionId,
                PaymentProofFileId = paymentProof.Id,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);

            // Notify SuperAdmin via Viber
            var superAdmin = await _db.Users
                .IgnoreQueryFilters()
                .Where(u => u.IsSuperAdmin && u.TelegramChatId != null)
                .Select(u => new { u.TelegramChatId, u.FullName })
                .FirstOrDefaultAsync(cancellationToken);

            if (superAdmin?.TelegramChatId != null)
            {
                var tenantName = await _db.Tenants
                    .IgnoreQueryFilters()
                    .Where(t => t.Id == user.TenantId)
                    .Select(t => t.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                var msg = $"💳 StockEasy\n\nNew payment request from {tenantName ?? user.FullName} " +
                          $"for {plan.Name} ({dto.BillingCycle}).\n\nPlease review it in the admin panel.";
                await _telegramService.SendMessageAsync(superAdmin.TelegramChatId, msg, cancellationToken);
            }

            TempData["SuccessMsg"]      = "Payment request submitted. SuperAdmin will review it before activating the subscription.";
            TempData["SuccessListUrl"]  = Url.Action("MyRequests", "SubscriptionRequest");
            TempData["SuccessListLabel"]= "View My Requests";
            return RedirectToAction(nameof(MyRequests));
        }

        public async Task<IActionResult> MyRequests()
        {
            var user = await RequireCurrentUserAsync();
            if (user == null) return RedirectToAction("Login", "Account");

            var requests = await _db.SubscriptionPaymentRequests
                .Where(r => r.TenantId == user.TenantId)
                .Include(r => r.PaymentProofFile)
                .OrderByDescending(r => r.SubmittedAt)
                .ToListAsync();

            return View(requests);
        }

        private async Task<ApplicationUser?> RequireCurrentUserAsync() => await _userManager.GetUserAsync(User);

        private async Task<SubscriptionPaymentPageDto> BuildPaymentPageDtoAsync(Guid tenantId, Plan plan, CreateSubscriptionPaymentRequestDto dto)
        {
            return new SubscriptionPaymentPageDto
            {
                PlanId = plan.Id,
                PlanName = plan.Name,
                PlanCode = plan.Code,
                BillingCycle = dto.BillingCycle,
                Price = dto.BillingCycle == "Yearly" ? plan.PriceYearly ?? plan.PriceMonthly : plan.PriceMonthly,
                CurrencyCode = (await _db.Tenants.Where(t => t.Id == tenantId).Select(t => t.CurrencyCode).FirstOrDefaultAsync()) ?? string.Empty,
                WalletDisplayName = _paymentSettings.WalletDisplayName,
                WalletAccountName = _paymentSettings.WalletAccountName,
                WalletAccountNo = _paymentSettings.WalletAccountNo,
                QrImageUrl = ResolveQrImageUrl(),
                Instructions = _paymentSettings.Instructions,
                Form = dto
            };
        }

        private string ResolveQrImageUrl() =>
            string.IsNullOrWhiteSpace(_paymentSettings.QrImageUrl)
                ? DefaultQrImageUrl
                : _paymentSettings.QrImageUrl;

        private static string NormalizeBillingCycle(string? billingCycle) =>
            string.Equals(billingCycle, "Yearly", StringComparison.OrdinalIgnoreCase) ? "Yearly" : "Monthly";
    }
}
