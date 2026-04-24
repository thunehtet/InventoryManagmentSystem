using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.ProductImport;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Subscription;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.ProductImport
{
    public class ProductImportService : IProductImportService
    {
        private const int MaxRows = 1000;
        private const long MaxFileSizeBytes = 2 * 1024 * 1024;
        private static readonly string[] RequiredHeaders =
        {
            "ProductName",
            "Category",
            "Brand",
            "Description",
            "SKU",
            "Color",
            "Size",
            "CostPrice",
            "SellingPrice",
            "Quantity",
            "ImageUrl"
        };

        private readonly AppDbContext _db;
        private readonly ISubscriptionService _subscriptionService;

        public ProductImportService(AppDbContext db, ISubscriptionService subscriptionService)
        {
            _db = db;
            _subscriptionService = subscriptionService;
        }

        public async Task<ProductImportResultDto> ImportAsync(
            Guid tenantId,
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            ValidateFile(file);

            List<Dictionary<string, string>> rows;
            await using (var stream = file.OpenReadStream())
            {
                rows = XlsxTable.ReadFirstWorksheet(stream);
            }

            var result = new ProductImportResultDto();
            if (rows.Count == 0)
            {
                result.Rows.Add(ErrorRow(1, "", "", "The Excel file is empty."));
                result.SkippedRows = 1;
                return result;
            }

            if (rows.Count > MaxRows)
            {
                result.Rows.Add(ErrorRow(1, "", "", $"The file has too many rows. Maximum allowed rows: {MaxRows}."));
                result.SkippedRows = rows.Count;
                result.TotalRows = rows.Count;
                return result;
            }

            result.TotalRows = rows.Count;

            var parsedRows = rows
                .Select((row, index) => ParseRow(index + 2, row))
                .ToList();

            foreach (var row in parsedRows.Where(x => !x.IsValid))
            {
                result.Rows.Add(ErrorRow(row.RowNumber, row.ProductName, row.VariantLabel, row.ErrorMessage));
            }

            var validRows = parsedRows.Where(x => x.IsValid).ToList();
            if (validRows.Count == 0)
            {
                result.SkippedRows = result.Rows.Count;
                return result;
            }

            var existingProducts = await _db.Products
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId)
                .Include(p => p.Variants)
                .ToListAsync(cancellationToken);

            var productMap = existingProducts
                .GroupBy(BuildProductKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var existingSkuSet = existingProducts
                .SelectMany(p => p.Variants)
                .Select(v => v.SKU)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var optionSet = existingProducts
                .SelectMany(p => p.Variants.Select(v => BuildOptionKey(BuildProductKey(p), v.Color, v.Size)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var productLimit = await _subscriptionService.GetProductLimitAsync(tenantId);
            var variantLimit = await _subscriptionService.GetVariantLimitAsync(tenantId);
            var productCountAfterImport = productLimit.Current;
            var variantCountAfterImport = variantLimit.Current;
            var importProductKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var importSkuSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rowsToImport = new List<ImportRow>();

            foreach (var row in validRows)
            {
                var productKey = BuildProductKey(row);
                var isNewProduct = !productMap.ContainsKey(productKey) && !importProductKeys.Contains(productKey);

                if (isNewProduct && productLimit.Max.HasValue && productCountAfterImport >= productLimit.Max.Value)
                {
                    result.Rows.Add(ErrorRow(row.RowNumber, row.ProductName, row.VariantLabel, $"Product limit reached ({productLimit.Current}/{productLimit.Max})."));
                    continue;
                }

                if (variantLimit.Max.HasValue && variantCountAfterImport >= variantLimit.Max.Value)
                {
                    result.Rows.Add(ErrorRow(row.RowNumber, row.ProductName, row.VariantLabel, $"Variant limit reached ({variantLimit.Current}/{variantLimit.Max})."));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(row.Sku))
                {
                    if (existingSkuSet.Contains(row.Sku) || !importSkuSet.Add(row.Sku))
                    {
                        result.Rows.Add(ErrorRow(row.RowNumber, row.ProductName, row.VariantLabel, $"SKU '{row.Sku}' already exists or is duplicated in this file."));
                        continue;
                    }
                }

                var optionKey = BuildOptionKey(productKey, row.Color, row.Size);
                if (!optionSet.Add(optionKey))
                {
                    result.Rows.Add(ErrorRow(row.RowNumber, row.ProductName, row.VariantLabel, "This product already has the same color/size option."));
                    continue;
                }

                if (isNewProduct)
                {
                    importProductKeys.Add(productKey);
                    productCountAfterImport++;
                }

                variantCountAfterImport++;
                rowsToImport.Add(row);
            }

            if (rowsToImport.Count == 0)
            {
                result.SkippedRows = result.Rows.Count;
                return result;
            }

            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var row in rowsToImport)
                {
                    var productKey = BuildProductKey(row);
                    if (!productMap.TryGetValue(productKey, out var product))
                    {
                        product = new Product
                        {
                            TenantId = tenantId,
                            Name = row.ProductName,
                            Category = row.Category,
                            Brand = row.Brand,
                            Description = row.Description,
                            ImageUrl = row.ImageUrl,
                            IsActive = true
                        };
                        _db.Products.Add(product);
                        await _db.SaveChangesAsync(cancellationToken);
                        productMap[productKey] = product;
                        result.CreatedProducts++;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(product.Description) && !string.IsNullOrWhiteSpace(row.Description))
                            product.Description = row.Description;
                        if (string.IsNullOrWhiteSpace(product.ImageUrl) && !string.IsNullOrWhiteSpace(row.ImageUrl))
                            product.ImageUrl = row.ImageUrl;
                    }

                    var sku = string.IsNullOrWhiteSpace(row.Sku)
                        ? await GenerateAutoSkuAsync(tenantId, existingSkuSet, cancellationToken)
                        : row.Sku;

                    existingSkuSet.Add(sku);

                    var variant = new ProductVariant
                    {
                        TenantId = tenantId,
                        ProductId = product.Id,
                        SKU = sku,
                        Color = row.Color,
                        Size = row.Size,
                        CostPrice = row.CostPrice,
                        SellingPrice = row.SellingPrice
                    };
                    _db.ProductVariants.Add(variant);
                    await _db.SaveChangesAsync(cancellationToken);
                    result.CreatedVariants++;

                    if (!string.IsNullOrWhiteSpace(row.ImageUrl))
                    {
                        _db.ProductVariantImages.Add(new ProductVariantImage
                        {
                            TenantId = tenantId,
                            ProductVariantId = variant.Id,
                            ImageUrl = row.ImageUrl,
                            SortOrder = 0,
                            IsPrimary = true
                        });
                    }

                    if (row.Quantity > 0)
                    {
                        _db.StockMovements.Add(new StockMovement
                        {
                            TenantId = tenantId,
                            ProductVariantId = variant.Id,
                            MovementType = "IN",
                            Quantity = row.Quantity,
                            MovementDate = DateTime.UtcNow,
                            Remarks = $"Opening stock from Excel import row {row.RowNumber}"
                        });
                        result.CreatedStockMovements++;
                    }

                    result.Rows.Add(new ProductImportRowResultDto
                    {
                        RowNumber = row.RowNumber,
                        ProductName = row.ProductName,
                        Variant = row.VariantLabel,
                        Status = "Imported",
                        Message = string.IsNullOrWhiteSpace(row.Sku)
                            ? $"Imported with auto code {sku}."
                            : "Imported."
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }

            result.ImportedRows = result.Rows.Count(x => x.Status == "Imported");
            result.SkippedRows = result.Rows.Count(x => x.Status == "Skipped");
            return result;
        }

        public byte[] BuildTemplate()
        {
            var rows = new List<string[]>
            {
                RequiredHeaders,
                new[] { "T-Shirt", "Shirt", "Own Brand", "Cotton daily wear", "", "Black", "M", "12000", "25000", "10", "" },
                new[] { "T-Shirt", "Shirt", "Own Brand", "Cotton daily wear", "", "Black", "L", "12000", "25000", "8", "" },
                new[] { "Floral Dress", "Dress", "Own Brand", "Simple product without size/color", "", "", "", "20000", "45000", "5", "" }
            };

            return XlsxTable.WriteWorksheet(rows);
        }

        private static void ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("Please choose an Excel file.");

            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException("Excel file is too large. Maximum allowed size is 2 MB.");

            var extension = Path.GetExtension(file.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only .xlsx Excel files are supported.");
        }

        private static ImportRow ParseRow(int rowNumber, Dictionary<string, string> values)
        {
            var row = new ImportRow
            {
                RowNumber = rowNumber,
                ProductName = Get(values, "ProductName").Trim(),
                Category = NormalizeCatalogValue(Get(values, "Category"), "General"),
                Brand = NormalizeCatalogValue(Get(values, "Brand"), "Own Brand"),
                Description = EmptyToNull(Get(values, "Description")),
                Sku = EmptyToNull(Get(values, "SKU")),
                Color = NormalizeVariantOption(Get(values, "Color")),
                Size = NormalizeVariantOption(Get(values, "Size")),
                ImageUrl = EmptyToNull(Get(values, "ImageUrl"))
            };

            row.VariantLabel = BuildVariantLabel(row.Color, row.Size);

            if (string.IsNullOrWhiteSpace(row.ProductName))
                return row.Invalid("ProductName is required.");

            if (!TryParseMoney(Get(values, "CostPrice"), out var costPrice) || costPrice < 0)
                return row.Invalid("CostPrice must be a zero or positive number.");
            row.CostPrice = costPrice;

            if (!TryParseMoney(Get(values, "SellingPrice"), out var sellingPrice) || sellingPrice <= 0)
                return row.Invalid("SellingPrice is required and must be greater than zero.");
            row.SellingPrice = sellingPrice;

            if (!TryParseMoney(Get(values, "Quantity"), out var quantity) || quantity < 0)
                return row.Invalid("Quantity must be a zero or positive number.");
            row.Quantity = quantity;

            if (!string.IsNullOrWhiteSpace(row.ImageUrl) &&
                !Uri.TryCreate(row.ImageUrl, UriKind.Absolute, out _))
            {
                return row.Invalid("ImageUrl must be a valid absolute URL.");
            }

            row.IsValid = true;
            return row;
        }

        private static ProductImportRowResultDto ErrorRow(
            int rowNumber,
            string productName,
            string variant,
            string message)
        {
            return new ProductImportRowResultDto
            {
                RowNumber = rowNumber,
                ProductName = productName,
                Variant = variant,
                Status = "Skipped",
                Message = message
            };
        }

        private async Task<string> GenerateAutoSkuAsync(
            Guid tenantId,
            HashSet<string> reservedSkus,
            CancellationToken cancellationToken)
        {
            var next = await _db.ProductVariants
                .IgnoreQueryFilters()
                .Where(v => v.TenantId == tenantId)
                .CountAsync(cancellationToken) + reservedSkus.Count + 1;

            while (true)
            {
                var sku = $"AUTO-{next:D6}";
                var exists = reservedSkus.Contains(sku) ||
                    await _db.ProductVariants
                        .IgnoreQueryFilters()
                        .AnyAsync(v => v.TenantId == tenantId && v.SKU == sku, cancellationToken);

                if (!exists)
                    return sku;

                next++;
            }
        }

        private static string Get(Dictionary<string, string> values, string key)
            => values.TryGetValue(NormalizeHeader(key), out var value) ? value : string.Empty;

        private static string BuildProductKey(Product product)
            => BuildProductKey(product.Name, product.Category, product.Brand);

        private static string BuildProductKey(ImportRow row)
            => BuildProductKey(row.ProductName, row.Category, row.Brand);

        private static string BuildProductKey(string name, string category, string brand)
            => $"{NormalizeKey(name)}|{NormalizeKey(category)}|{NormalizeKey(brand)}";

        private static string BuildOptionKey(string productKey, string color, string size)
            => $"{productKey}|{NormalizeKey(color)}|{NormalizeKey(size)}";

        private static string NormalizeHeader(string value)
            => Regex.Replace(value ?? string.Empty, @"[\s_\-]", "").Trim().ToLowerInvariant();

        private static string NormalizeKey(string value)
            => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim().ToLowerInvariant();

        private static string NormalizeCatalogValue(string? value, string fallback)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        private static string NormalizeVariantOption(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? "Default" : trimmed;
        }

        private static string? EmptyToNull(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static string BuildVariantLabel(string color, string size)
        {
            var parts = new[] { color, size }
                .Where(x => !string.IsNullOrWhiteSpace(x) &&
                            !string.Equals(x, "Default", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return parts.Count == 0 ? "Standard" : string.Join(" / ", parts);
        }

        private static bool TryParseMoney(string? value, out int result)
        {
            result = 0;
            var trimmed = value?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return true;

            trimmed = trimmed.Replace(",", "");
            if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                result = (int)Math.Round(parsed, MidpointRounding.AwayFromZero);
                return true;
            }

            return false;
        }

        private sealed class ImportRow
        {
            public int RowNumber { get; init; }
            public string ProductName { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? Sku { get; set; }
            public string Color { get; set; } = "Default";
            public string Size { get; set; } = "Default";
            public int CostPrice { get; set; }
            public int SellingPrice { get; set; }
            public int Quantity { get; set; }
            public string? ImageUrl { get; set; }
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public string VariantLabel { get; set; } = string.Empty;

            public ImportRow Invalid(string message)
            {
                ErrorMessage = message;
                IsValid = false;
                return this;
            }
        }

        private static class XlsxTable
        {
            private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            public static List<Dictionary<string, string>> ReadFirstWorksheet(Stream stream)
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                var sharedStrings = ReadSharedStrings(archive);
                var sheetPath = GetFirstSheetPath(archive);
                var sheetEntry = archive.GetEntry(sheetPath)
                    ?? throw new InvalidOperationException("The workbook does not contain a worksheet.");

                using var sheetStream = sheetEntry.Open();
                var sheetDoc = XDocument.Load(sheetStream);
                var rowElements = sheetDoc
                    .Descendants(SpreadsheetNs + "sheetData")
                    .Descendants(SpreadsheetNs + "row")
                    .ToList();

                if (rowElements.Count == 0)
                    return new List<Dictionary<string, string>>();

                var tableRows = rowElements
                    .Select(r => ReadRow(r, sharedStrings))
                    .Where(r => r.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                    .ToList();

                if (tableRows.Count <= 1)
                    return new List<Dictionary<string, string>>();

                var headers = tableRows[0]
                    .OrderBy(x => x.Key)
                    .Select(x => NormalizeHeader(x.Value))
                    .ToList();

                var result = new List<Dictionary<string, string>>();
                foreach (var row in tableRows.Skip(1))
                {
                    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < headers.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(headers[i]))
                            continue;

                        values[headers[i]] = row.TryGetValue(i + 1, out var value) ? value : string.Empty;
                    }

                    if (values.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                        result.Add(values);
                }

                return result;
            }

            public static byte[] WriteWorksheet(List<string[]> rows)
            {
                using var stream = new MemoryStream();
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    WriteEntry(archive, "[Content_Types].xml",
                        """
                        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                          <Default Extension="xml" ContentType="application/xml"/>
                          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                        </Types>
                        """);
                    WriteEntry(archive, "_rels/.rels",
                        """
                        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                        </Relationships>
                        """);
                    WriteEntry(archive, "xl/_rels/workbook.xml.rels",
                        """
                        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                        </Relationships>
                        """);
                    WriteEntry(archive, "xl/workbook.xml",
                        """
                        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                          <sheets>
                            <sheet name="Products" sheetId="1" r:id="rId1"/>
                          </sheets>
                        </workbook>
                        """);
                    WriteEntry(archive, "xl/styles.xml",
                        """
                        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                          <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
                          <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
                          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                          <cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>
                        </styleSheet>
                        """);
                    WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheetXml(rows));
                }

                return stream.ToArray();
            }

            private static List<string> ReadSharedStrings(ZipArchive archive)
            {
                var entry = archive.GetEntry("xl/sharedStrings.xml");
                if (entry == null)
                    return new List<string>();

                using var stream = entry.Open();
                var doc = XDocument.Load(stream);
                return doc.Descendants(SpreadsheetNs + "si")
                    .Select(si => string.Concat(si.Descendants(SpreadsheetNs + "t").Select(t => t.Value)))
                    .ToList();
            }

            private static string GetFirstSheetPath(ZipArchive archive)
            {
                var workbookEntry = archive.GetEntry("xl/workbook.xml")
                    ?? throw new InvalidOperationException("The Excel file is missing workbook.xml.");
                var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
                    ?? throw new InvalidOperationException("The Excel file is missing workbook relationships.");

                XDocument workbookDoc;
                using (var stream = workbookEntry.Open())
                    workbookDoc = XDocument.Load(stream);

                var firstSheet = workbookDoc
                    .Descendants(SpreadsheetNs + "sheet")
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException("The workbook does not contain any sheets.");

                var relationshipId = firstSheet.Attribute(RelationshipNs + "id")?.Value
                    ?? throw new InvalidOperationException("The first worksheet relationship is missing.");

                XDocument relationshipsDoc;
                using (var stream = relationshipsEntry.Open())
                    relationshipsDoc = XDocument.Load(stream);

                var target = relationshipsDoc
                    .Descendants(PackageRelationshipNs + "Relationship")
                    .FirstOrDefault(r => string.Equals(r.Attribute("Id")?.Value, relationshipId, StringComparison.OrdinalIgnoreCase))
                    ?.Attribute("Target")?.Value;

                if (string.IsNullOrWhiteSpace(target))
                    throw new InvalidOperationException("The first worksheet target is missing.");

                return target.StartsWith("/", StringComparison.Ordinal)
                    ? target.TrimStart('/')
                    : "xl/" + target.TrimStart('/');
            }

            private static Dictionary<int, string> ReadRow(XElement row, List<string> sharedStrings)
            {
                var values = new Dictionary<int, string>();
                var nextColumn = 1;
                foreach (var cell in row.Elements(SpreadsheetNs + "c"))
                {
                    var column = GetColumnIndex(cell.Attribute("r")?.Value) ?? nextColumn;
                    nextColumn = column + 1;
                    values[column] = ReadCell(cell, sharedStrings);
                }

                return values;
            }

            private static string ReadCell(XElement cell, List<string> sharedStrings)
            {
                var type = cell.Attribute("t")?.Value;
                if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
                {
                    var raw = cell.Element(SpreadsheetNs + "v")?.Value;
                    return int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count
                        ? sharedStrings[index]
                        : string.Empty;
                }

                if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Concat(cell.Descendants(SpreadsheetNs + "t").Select(t => t.Value));
                }

                return cell.Element(SpreadsheetNs + "v")?.Value ?? string.Empty;
            }

            private static int? GetColumnIndex(string? cellReference)
            {
                if (string.IsNullOrWhiteSpace(cellReference))
                    return null;

                var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
                if (letters.Length == 0)
                    return null;

                var sum = 0;
                foreach (var letter in letters)
                    sum = sum * 26 + (letter - 'A' + 1);

                return sum;
            }

            private static string BuildSheetXml(List<string[]> rows)
            {
                var sheetRows = rows.Select((row, rowIndex) =>
                    new XElement(SpreadsheetNs + "row",
                        new XAttribute("r", rowIndex + 1),
                        row.Select((value, columnIndex) =>
                            new XElement(SpreadsheetNs + "c",
                                new XAttribute("r", ToCellRef(columnIndex + 1, rowIndex + 1)),
                                new XAttribute("t", "inlineStr"),
                                new XElement(SpreadsheetNs + "is",
                                    new XElement(SpreadsheetNs + "t", value ?? string.Empty))))));

                var worksheet = new XElement(SpreadsheetNs + "worksheet",
                    new XAttribute(XNamespace.Xmlns + "r", RelationshipNs),
                    new XElement(SpreadsheetNs + "sheetData", sheetRows));

                return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), worksheet).ToString(SaveOptions.DisableFormatting);
            }

            private static string ToCellRef(int column, int row)
            {
                var dividend = column;
                var columnName = string.Empty;
                while (dividend > 0)
                {
                    var modulo = (dividend - 1) % 26;
                    columnName = Convert.ToChar('A' + modulo) + columnName;
                    dividend = (dividend - modulo) / 26;
                }

                return columnName + row.ToString(CultureInfo.InvariantCulture);
            }

            private static void WriteEntry(ZipArchive archive, string path, string content)
            {
                var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
    }
}
