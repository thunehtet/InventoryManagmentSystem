using System.Data;
using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Order;
using ClothInventoryApp.Filters;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Stock;
using ClothInventoryApp.Services.Tenant;
using ClothInventoryApp.Services.Usage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    /// <summary>
    /// Tenant-admin side for managing storefront orders. Customer-facing storefront logic
    /// lives in StorefrontController (anonymous, tenant resolved from URL).
    /// </summary>
    [Authorize]
    [FeatureRequired("storefront")]
    public class OrdersController : TenantAwareController
    {
        private readonly IStockService _stockService;
        private readonly IUsageTrackingService _usageTrackingService;

        private static readonly string[] ValidStatuses =
            { "Pending", "Confirmed", "Shipped", "Delivered", "Cancelled" };

        public OrdersController(
            AppDbContext context,
            ITenantProvider tenantProvider,
            IStockService stockService,
            IUsageTrackingService usageTrackingService)
            : base(context, tenantProvider)
        {
            _stockService = stockService;
            _usageTrackingService = usageTrackingService;
        }

        // GET /Orders
        public async Task<IActionResult> Index(string? status, string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            page = Math.Max(1, page);

            var query = _context.ShopOrders.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && ValidStatuses.Contains(status))
                query = query.Where(o => o.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                query = query.Where(o =>
                    o.OrderNumber.Contains(kw) ||
                    o.CustomerName.Contains(kw) ||
                    o.CustomerPhone.Contains(kw));
            }

            var total = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(o => new OrderListItemDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    ItemCount = o.Items.Count,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            // Status counts for summary cards
            var counts = await _context.ShopOrders
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.StatusCounts = counts.ToDictionary(x => x.Status, x => x.Count);
            ViewBag.Search = search;
            ViewBag.StatusFilter = status;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = size,
                TotalCount = total,
                Action = nameof(Index),
                Extra = new()
                {
                    ["search"] = search,
                    ["status"] = status
                }
            };

            return View(orders);
        }

        // GET /Orders/Details/{id}
        public async Task<IActionResult> Details(Guid id)
        {
            var order = await LoadOrderWithItems(id);
            if (order == null) return NotFound();

            return View(MapToDetailsDto(order));
        }

        // POST /Orders/Confirm/{id}
        // Transitions Pending → Confirmed and creates a Sale + SaleItems to feed revenue reports.
        // Stock was already decremented at checkout via StockMovement OUT rows, so we don't create
        // new movements here — we re-tag the existing ones with the new SaleId so admin delete
        // flows (which cascade on SaleId) still behave correctly.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(Guid id)
        {
            var tenantId = _tenantProvider.GetTenantId();

            var order = await _context.ShopOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status != "Pending")
            {
                TempData["Error"] = this.LocalizeShared("Only pending orders can be confirmed.");
                return RedirectToAction(nameof(Details), new { id });
            }

            // Pull variant cost prices so we can compute profit on the Sale record
            var variantIds = order.Items
                .Where(i => i.ProductVariantId.HasValue)
                .Select(i => i.ProductVariantId!.Value)
                .Distinct()
                .ToList();

            var variantCosts = await _context.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v.CostPrice);

            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                var sale = new Sale
                {
                    TenantId = tenantId,
                    SaleDate = DateTime.UtcNow,
                    CustomerId = order.CustomerId,
                    Discount = order.Discount,
                    Items = order.Items.Select(i =>
                    {
                        var cost = i.ProductVariantId.HasValue && variantCosts.TryGetValue(i.ProductVariantId.Value, out var c) ? c : 0;
                        return new SaleItem
                        {
                            TenantId = tenantId,
                            ProductVariantId = i.ProductVariantId,
                            ProductNameSnapshot = i.ProductNameSnapshot,
                            ProductSkuSnapshot = i.ProductSkuSnapshot,
                            ProductColorSnapshot = i.ProductColorSnapshot,
                            ProductSizeSnapshot = i.ProductSizeSnapshot,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                            CostPrice = cost,
                            Profit = (i.UnitPrice - cost) * i.Quantity
                        };
                    }).ToList()
                };

                sale.TotalAmount = sale.Items.Sum(x => x.Quantity * x.UnitPrice) - sale.Discount;
                sale.TotalProfit = sale.Items.Sum(x => x.Profit) - sale.Discount;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Re-link the StockMovement rows created at checkout to this Sale so admin
                // deletion of the Sale cleans them up consistently with the Sales module.
                var movements = await _context.StockMovements
                    .Where(m => m.TenantId == tenantId
                             && m.MovementType == "OUT"
                             && m.Remarks == $"Storefront order {order.OrderNumber}")
                    .ToListAsync();
                foreach (var m in movements)
                {
                    m.SaleId = sale.Id;
                    m.Remarks = $"Sale #{sale.Id} (order {order.OrderNumber})";
                }

                order.SaleId = sale.Id;
                order.Status = "Confirmed";
                order.ConfirmedAt = DateTime.UtcNow;

                // If already marked paid, emit the cash transaction now
                if (order.PaymentStatus == "Paid")
                {
                    _context.CashTransactions.Add(new CashTransaction
                    {
                        TenantId = tenantId,
                        TransactionDate = DateTime.UtcNow,
                        Type = "IN",
                        Category = "Sale Income",
                        Amount = sale.TotalAmount,
                        SaleId = sale.Id,
                        ReferenceNo = order.OrderNumber,
                        Remarks = $"Storefront order {order.OrderNumber}"
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                await _usageTrackingService.TrackActionAsync(tenantId, "storefront", "confirm", "ShopOrder", order.Id.ToString(), $"Confirmed storefront order {order.OrderNumber}.", cancellationToken: HttpContext.RequestAborted);
                TempData["SuccessMsg"] = this.LocalizeShared("Order confirmed.");
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = this.LocalizeShared("Failed to confirm the order. Please try again.");
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Orders/Ship/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Ship(Guid id)
        {
            var order = await _context.ShopOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.Status != "Confirmed")
            {
                TempData["Error"] = this.LocalizeShared("Only confirmed orders can be marked as shipped.");
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = "Shipped";
            order.ShippedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _usageTrackingService.TrackActionAsync(order.TenantId, "storefront", "ship", "ShopOrder", order.Id.ToString(), $"Marked order {order.OrderNumber} as shipped.", cancellationToken: HttpContext.RequestAborted);
            TempData["SuccessMsg"] = this.LocalizeShared("Order marked as shipped.");
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Orders/Deliver/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Deliver(Guid id)
        {
            var order = await _context.ShopOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.Status != "Shipped" && order.Status != "Confirmed")
            {
                TempData["Error"] = this.LocalizeShared("This order cannot be marked as delivered from its current status.");
                return RedirectToAction(nameof(Details), new { id });
            }

            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                order.Status = "Delivered";
                order.DeliveredAt = DateTime.UtcNow;

                // Cash-on-delivery: mark paid and emit the cash transaction if not yet done
                if (order.PaymentStatus != "Paid")
                {
                    order.PaymentStatus = "Paid";
                    if (order.SaleId.HasValue)
                    {
                        var alreadyBooked = await _context.CashTransactions
                            .AnyAsync(c => c.SaleId == order.SaleId.Value);
                        if (!alreadyBooked)
                        {
                            _context.CashTransactions.Add(new CashTransaction
                            {
                                TenantId = order.TenantId,
                                TransactionDate = DateTime.UtcNow,
                                Type = "IN",
                                Category = "Sale Income",
                                Amount = order.TotalAmount,
                                SaleId = order.SaleId,
                                ReferenceNo = order.OrderNumber,
                                Remarks = $"Storefront order {order.OrderNumber} (COD)"
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = this.LocalizeShared("Failed to mark delivery. Please try again.");
                return RedirectToAction(nameof(Details), new { id });
            }

            await _usageTrackingService.TrackActionAsync(order.TenantId, "storefront", "deliver", "ShopOrder", order.Id.ToString(), $"Marked order {order.OrderNumber} as delivered.", cancellationToken: HttpContext.RequestAborted);
            TempData["SuccessMsg"] = this.LocalizeShared("Order marked as delivered.");
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Orders/MarkPaid/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(Guid id)
        {
            var order = await _context.ShopOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.PaymentStatus == "Paid")
            {
                TempData["Error"] = this.LocalizeShared("This order is already marked as paid.");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (order.Status == "Cancelled")
            {
                TempData["Error"] = this.LocalizeShared("Cancelled orders cannot be marked as paid.");
                return RedirectToAction(nameof(Details), new { id });
            }

            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                order.PaymentStatus = "Paid";

                if (order.SaleId.HasValue)
                {
                    var alreadyBooked = await _context.CashTransactions
                        .AnyAsync(c => c.SaleId == order.SaleId.Value);
                    if (!alreadyBooked)
                    {
                        _context.CashTransactions.Add(new CashTransaction
                        {
                            TenantId = order.TenantId,
                            TransactionDate = DateTime.UtcNow,
                            Type = "IN",
                            Category = "Sale Income",
                            Amount = order.TotalAmount,
                            SaleId = order.SaleId,
                            ReferenceNo = order.OrderNumber,
                            Remarks = $"Storefront order {order.OrderNumber}"
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = this.LocalizeShared("Failed to mark order as paid. Please try again.");
                return RedirectToAction(nameof(Details), new { id });
            }

            await _usageTrackingService.TrackActionAsync(order.TenantId, "storefront", "mark_paid", "ShopOrder", order.Id.ToString(), $"Marked order {order.OrderNumber} as paid.", cancellationToken: HttpContext.RequestAborted);
            TempData["SuccessMsg"] = this.LocalizeShared("Payment recorded.");
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Orders/Cancel/{id}
        // Reverses the OUT stock movements created at checkout so the inventory is released
        // back to available stock. If a Sale was already created (order was confirmed) we
        // remove it and any associated cash transactions — this keeps reports clean.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid id, string? reason)
        {
            var tenantId = _tenantProvider.GetTenantId();

            var order = await _context.ShopOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.Status == "Cancelled")
            {
                TempData["Error"] = this.LocalizeShared("This order is already cancelled.");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (order.Status == "Delivered")
            {
                TempData["Error"] = this.LocalizeShared("Delivered orders cannot be cancelled.");
                return RedirectToAction(nameof(Details), new { id });
            }

            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                // Reverse the OUT movements by inserting matching IN movements so historical
                // OUTs remain auditable and current stock goes back up.
                var outMovements = await _context.StockMovements
                    .Where(m => m.TenantId == tenantId
                             && m.MovementType == "OUT"
                             && (m.Remarks == $"Storefront order {order.OrderNumber}"
                                 || (order.SaleId.HasValue && m.SaleId == order.SaleId)))
                    .ToListAsync();

                foreach (var m in outMovements)
                {
                    _context.StockMovements.Add(new StockMovement
                    {
                        TenantId = tenantId,
                        ProductVariantId = m.ProductVariantId,
                        MovementType = "IN",
                        Quantity = m.Quantity,
                        MovementDate = DateTime.UtcNow,
                        Remarks = $"Cancel storefront order {order.OrderNumber}"
                    });
                }

                // If a Sale was created on confirmation, remove it and any cash transactions
                if (order.SaleId.HasValue)
                {
                    var cashTxns = await _context.CashTransactions
                        .Where(c => c.SaleId == order.SaleId.Value)
                        .ToListAsync();
                    _context.CashTransactions.RemoveRange(cashTxns);

                    var sale = await _context.Sales
                        .Include(s => s.Items)
                        .FirstOrDefaultAsync(s => s.Id == order.SaleId.Value);
                    if (sale != null)
                    {
                        // Break the FK from StockMovement → Sale before removing the sale
                        var linkedMovements = await _context.StockMovements
                            .Where(m => m.SaleId == sale.Id)
                            .ToListAsync();
                        foreach (var lm in linkedMovements)
                            lm.SaleId = null;

                        _context.SaleItems.RemoveRange(sale.Items);
                        _context.Sales.Remove(sale);
                    }

                    order.SaleId = null;
                }

                order.Status = "Cancelled";
                order.CancelledAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    var truncated = reason.Trim();
                    if (truncated.Length > 500) truncated = truncated[..500];
                    order.InternalNotes = truncated;
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = this.LocalizeShared("Failed to cancel the order. Please try again.");
                return RedirectToAction(nameof(Details), new { id });
            }

            await _usageTrackingService.TrackActionAsync(tenantId, "storefront", "cancel", "ShopOrder", order.Id.ToString(), $"Cancelled storefront order {order.OrderNumber}.", cancellationToken: HttpContext.RequestAborted);
            TempData["SuccessMsg"] = this.LocalizeShared("Order cancelled and stock restored.");
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Orders/SaveNotes/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNotes(Guid id, string? internalNotes)
        {
            var order = await _context.ShopOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            var trimmed = internalNotes?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 500)
                trimmed = trimmed[..500];
            order.InternalNotes = string.IsNullOrEmpty(trimmed) ? null : trimmed;

            await _context.SaveChangesAsync();
            TempData["SuccessMsg"] = this.LocalizeShared("Notes saved.");
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Helpers ──────────────────────────────────────────────────
        private async Task<ShopOrder?> LoadOrderWithItems(Guid id)
        {
            return await _context.ShopOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        private static OrderDetailsDto MapToDetailsDto(ShopOrder order)
        {
            return new OrderDetailsDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                PublicToken = order.PublicToken,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                CustomerEmail = order.CustomerEmail,
                ShippingAddress = order.ShippingAddress,
                CustomerNotes = order.CustomerNotes,
                InternalNotes = order.InternalNotes,
                Subtotal = order.Subtotal,
                Discount = order.Discount,
                ShippingFee = order.ShippingFee,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                SaleId = order.SaleId,
                CreatedAt = order.CreatedAt,
                ConfirmedAt = order.ConfirmedAt,
                ShippedAt = order.ShippedAt,
                DeliveredAt = order.DeliveredAt,
                CancelledAt = order.CancelledAt,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductVariantId = i.ProductVariantId,
                    ProductName = i.ProductNameSnapshot,
                    Sku = i.ProductSkuSnapshot,
                    Color = i.ProductColorSnapshot,
                    Size = i.ProductSizeSnapshot,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal
                }).ToList()
            };
        }
    }
}
