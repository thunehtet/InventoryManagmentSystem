using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Sale;
using ClothInventoryApp.Filters;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Feature;
using ClothInventoryApp.Services.Stock;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using System.Data;
using System.Security.Cryptography;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    [FeatureRequired("sales")]
    public class SalesController : TenantAwareController
    {
        private readonly IFeatureService _featureService;
        private readonly IStockService _stockService;

        public SalesController(
            AppDbContext context,
            ITenantProvider tenantProvider,
            IFeatureService featureService,
            IStockService stockService)
            : base(context, tenantProvider)
        {
            _featureService = featureService;
            _stockService = stockService;
        }
       

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _context.Sales.Include(s => s.Customer).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s =>
                    s.Customer != null && s.Customer.Name.Contains(search));

            var total = await query.CountAsync();
            var sales = await query
                .OrderByDescending(s => s.SaleDate)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(s => new ViewSaleDto
                {
                    Id = s.Id,
                    SaleDate = s.SaleDate,
                    TotalAmount = s.TotalAmount,
                    TotalProfit = s.TotalProfit,
                    Discount = s.Discount,
                    CustomerId = s.CustomerId,
                    CustomerName = s.Customer != null ? s.Customer.Name : null
                })
                .ToListAsync();

            var tid = _tenantProvider.GetTenantId();
            ViewBag.HasSaleProfit = await _featureService.HasFeatureAsync(tid, "sale_profit");
            ViewBag.HasCustomers  = await _featureService.HasFeatureAsync(tid, "customers");
            ViewBag.Search = search;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["search"] = search }
            };
            return View(sales);
        }

        public async Task<IActionResult> Create()
        {
            var tid = _tenantProvider.GetTenantId();
            var hasCustomers = await _featureService.HasFeatureAsync(tid, "customers");
            ViewBag.HasCustomers = hasCustomers;

            await LoadVariantDropDown();
            if (hasCustomers) await LoadCustomerDropDown();
            return View(new CreateSaleDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSaleDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadVariantDropDown();
                await LoadCustomerDropDown();
                return View(dto);
            }

            var tenantId = _tenantProvider.GetTenantId();
            dto.Items = dto.Items
                .Where(x =>  x.Quantity > 0)
                .ToList();

            if (dto.CustomerId.HasValue &&
                !await _context.Customers.AnyAsync(c => c.Id == dto.CustomerId.Value && c.TenantId == tenantId && c.IsActive))
            {
                ModelState.AddModelError(nameof(dto.CustomerId), "Please select a valid active customer in your workspace.");
                await LoadVariantDropDown();
                await LoadCustomerDropDown();
                return View(dto);
            }

            if (!dto.Items.Any())
            {
                ModelState.AddModelError("", "Please add at least one sale item.");
                await LoadVariantDropDown();
                await LoadCustomerDropDown();
                return View(dto);
            }

            var variantIds = dto.Items
                .Select(x => x.ProductVariantId)
                .Distinct()
                .ToList();

            var variantMap = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            var invalidVariantExists = dto.Items.Any(i =>
                !variantMap.TryGetValue(i.ProductVariantId, out var variant) ||
                variant.TenantId != tenantId ||
                !variant.Product.IsActive);

            if (invalidVariantExists)
            {
                ModelState.AddModelError("", "One or more selected variants are invalid or inactive.");
                await LoadVariantDropDown();
                await LoadCustomerDropDown();
                return View(dto);
            }

            var requestedQuantities = dto.Items
                .GroupBy(x => x.ProductVariantId)
                .Select(g => new
                {
                    ProductVariantId = g.Key,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToList();

            foreach (var item in requestedQuantities)
            {
                if (!await _stockService.CanApplyMovementAsync(
                    tenantId,
                    item.ProductVariantId,
                    "OUT",
                    item.Quantity))
                {
                    ModelState.AddModelError("", $"Not enough stock for variant ID {item.ProductVariantId}.");
                    await LoadVariantDropDown();
                    await LoadCustomerDropDown();
                    return View(dto);
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                foreach (var item in requestedQuantities)
                {
                    if (!await _stockService.CanApplyMovementAsync(
                        tenantId,
                        item.ProductVariantId,
                        "OUT",
                        item.Quantity))
                    {
                        throw new InvalidOperationException(
                            $"Not enough stock for variant ID {item.ProductVariantId}.");
                    }
                }

                var discount = Math.Max(0, dto.Discount);
                var subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                if (discount > subtotal)
                    throw new InvalidOperationException("Discount cannot exceed the sale subtotal.");

                var sale = new Sale
                {
                    SaleDate = dto.SaleDate,
                    TenantId = tenantId,
                    CustomerId = dto.CustomerId,
                    Discount = discount,
                    Items = dto.Items.Select(i => new SaleItem
                    {
                        ProductVariantId = i.ProductVariantId,
                        TenantId = tenantId,
                        ProductNameSnapshot = variantMap[i.ProductVariantId].Product.Name,
                        ProductSkuSnapshot = variantMap[i.ProductVariantId].SKU,
                        ProductColorSnapshot = variantMap[i.ProductVariantId].Color,
                        ProductSizeSnapshot = variantMap[i.ProductVariantId].Size,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        CostPrice = variantMap[i.ProductVariantId].CostPrice,
                        Profit = (i.UnitPrice - variantMap[i.ProductVariantId].CostPrice) * i.Quantity
                    }).ToList()
                };

                sale.TotalAmount = sale.Items.Sum(x => x.Quantity * x.UnitPrice) - discount;
                sale.TotalProfit = sale.Items.Sum(x => x.Profit) - discount;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in sale.Items)
                {
                    _context.StockMovements.Add(new StockMovement
                    {
                        ProductVariantId = item.ProductVariantId,
                        TenantId = tenantId,
                        Quantity = item.Quantity,
                        MovementType = "OUT",
                        MovementDate = DateTime.UtcNow,
                        SaleId = sale.Id,
                        Remarks = $"Sale #{sale.Id}"
                    });
                }

                _context.CashTransactions.Add(new CashTransaction
                {
                    TransactionDate = sale.SaleDate,
                    TenantId = tenantId,
                    Type = "IN",
                    Category = "Sale Income",
                    Amount = sale.TotalAmount,
                    SaleId = sale.Id,
                    ReferenceNo = $"Sale #{sale.Id}",
                    Remarks = "Auto generated from sale"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMsg"]      = "Sale recorded successfully.";
                TempData["SuccessListUrl"]  = Url.Action("Index", "Sales");
                TempData["SuccessListLabel"]= "View Sales";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", ex.Message);
                await LoadVariantDropDown();
                await LoadCustomerDropDown();
                return View(dto);
            }
            catch
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "An error occurred while saving the sale.");
                await LoadVariantDropDown();
                await LoadCustomerDropDown();
                return View(dto);
            }
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return NotFound();

            var dto = new ViewSaleDto
            {
                Id = sale.Id,
                SaleDate = sale.SaleDate,
                TotalAmount = sale.TotalAmount,
                TotalProfit = sale.TotalProfit,
                Discount = sale.Discount,
                CustomerId = sale.CustomerId,
                CustomerName = sale.Customer?.Name,
                CustomerPhone = sale.Customer?.Phone,
                CustomerAddress = sale.Customer?.Address,
                PublicReceiptUrl = Url.Action(
                    "Receipt",
                    "Public",
                    new { token = await EnsurePublicReceiptTokenAsync(sale) },
                    Request.Scheme),
                Items = sale.Items.Select(i => new ViewSaleItemDto
                {
                    Id = i.Id,
                    ProductVariantId = i.ProductVariantId,
                    ProductVariantName = FormatSaleItemName(i),
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    CostPrice = i.CostPrice,
                    LineTotal = i.Quantity * i.UnitPrice,
                    LineProfit = i.Profit
                }).ToList()
            };

            return View(dto);
        }

        public async Task<IActionResult> InvoicePdf(Guid id)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null) return NotFound();

            var tenantId = _tenantProvider.GetTenantId();
            var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);

            var dto = new ViewSaleDto
            {
                Id = sale.Id,
                SaleDate = sale.SaleDate,
                TotalAmount = sale.TotalAmount,
                TotalProfit = sale.TotalProfit,
                Discount = sale.Discount,
                CustomerId = sale.CustomerId,
                CustomerName = sale.Customer?.Name,
                CustomerPhone = sale.Customer?.Phone,
                CustomerAddress = sale.Customer?.Address,
                Items = sale.Items.Select(i => new ViewSaleItemDto
                {
                    Id = i.Id,
                    ProductVariantId = i.ProductVariantId,
                    ProductVariantName = FormatSaleItemName(i),
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    CostPrice = i.CostPrice,
                    LineTotal = i.Quantity * i.UnitPrice,
                    LineProfit = i.Profit
                }).ToList()
            };

            var doc = new ClothInventoryApp.Services.Pdf.InvoiceDocument(dto, tenant);
            var bytes = doc.GeneratePdf();
            var invoiceNo = id.ToString("N")[..8].ToUpper();
            return File(bytes, "application/pdf", $"Invoice-{invoiceNo}.pdf");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return NotFound();

            var dto = new ViewSaleDto
            {
                Id = sale.Id,
                SaleDate = sale.SaleDate,
                TotalAmount = sale.TotalAmount,
                TotalProfit = sale.TotalProfit,
                Discount = sale.Discount,
                CustomerId = sale.CustomerId,
                CustomerName = sale.Customer?.Name,
                Items = sale.Items.Select(i => new ViewSaleItemDto
                {
                    Id = i.Id,
                    ProductVariantId = i.ProductVariantId,
                    ProductVariantName = FormatSaleItemName(i),
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    CostPrice = i.CostPrice,
                    LineTotal = i.Quantity * i.UnitPrice,
                    LineProfit = i.Profit
                }).ToList()
            };

            return View(dto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return NotFound();

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Delete stock OUT movements linked to this sale
                var stockMovements = await _context.StockMovements
                    .Where(m => m.SaleId == id)
                    .ToListAsync();
                _context.StockMovements.RemoveRange(stockMovements);

                // Delete cash transactions linked to this sale
                var cashTxns = await _context.CashTransactions
                    .Where(c => c.SaleId == id)
                    .ToListAsync();
                _context.CashTransactions.RemoveRange(cashTxns);

                // Delete sale items and the sale itself
                _context.SaleItems.RemoveRange(sale.Items);
                _context.Sales.Remove(sale);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Failed to delete sale. Please try again.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetVariants(string? q = null, string? category = null, int page = 1, int pageSize = 48)
        {
            var tenantId = _tenantProvider.GetTenantId();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 12, 80);

            var query = _context.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                .Where(v => v.Product.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) &&
                !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(v => v.Product.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(v =>
                    v.Product.Name.Contains(keyword) ||
                    v.SKU.Contains(keyword) ||
                    v.Color.Contains(keyword) ||
                    v.Size.Contains(keyword) ||
                    (v.Product.Category != null && v.Product.Category.Contains(keyword)));
            }

            var total = await query.CountAsync();

            var variants = await query
                .OrderBy(v => v.Product.Category)
                .ThenBy(v => v.Product.Name)
                .ThenBy(v => v.SKU)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var stockMap = await _stockService.GetCurrentStockMapAsync(
                tenantId,
                variants.Select(v => v.Id));

            var categories = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive && !string.IsNullOrWhiteSpace(p.Category))
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var items = variants.Select(v => new
            {
                id = v.Id,
                productName = v.Product.Name,
                category = v.Product.Category,
                sku = v.SKU,
                size = v.Size,
                color = v.Color,
                sellingPrice = v.SellingPrice,
                costPrice = v.CostPrice,
                stock = stockMap.TryGetValue(v.Id, out var s) ? s : 0
            });

            return Json(new
            {
                total,
                page,
                pageSize,
                hasMore = page * pageSize < total,
                categories,
                items
            });
        }

        private async Task LoadVariantDropDown()
        {
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => v.Product.IsActive)
                .OrderBy(v => v.SKU)
                .Select(v => new
                {
                    v.Id,
                    Text = v.Product.Name + " - " + v.SKU + " - " + v.Color + " - " + v.Size
                })
                .ToListAsync();

            ViewBag.ProductVariants = new SelectList(variants, "Id", "Text");
        }

        private async Task LoadCustomerDropDown()
        {
            var customers = await _context.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            ViewBag.Customers = new SelectList(customers, "Id", "Name");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshReceiptLink(Guid id)
        {
            var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null)
                return NotFound();

            sale.PublicReceiptToken = GeneratePublicToken();
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<string> EnsurePublicReceiptTokenAsync(Sale sale)
        {
            if (!string.IsNullOrWhiteSpace(sale.PublicReceiptToken))
                return sale.PublicReceiptToken;

            sale.PublicReceiptToken = GeneratePublicToken();
            await _context.SaveChangesAsync();
            return sale.PublicReceiptToken;
        }

        private static string GeneratePublicToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        }

        private static string FormatSaleItemName(SaleItem item)
        {
            return string.Join(" / ", new[]
            {
                item.ProductNameSnapshot,
                item.ProductSkuSnapshot,
                item.ProductColorSnapshot,
                item.ProductSizeSnapshot
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }
}
