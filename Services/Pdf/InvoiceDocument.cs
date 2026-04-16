using ClothInventoryApp.Dto.Sale;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TenantModel = ClothInventoryApp.Models.Tenant;

namespace ClothInventoryApp.Services.Pdf
{
    public class InvoiceDocument : IDocument
    {
        private static readonly string[] InvoiceFontFamily = ["Myanmar Text", "Arial", "Helvetica", "sans-serif"];
        private readonly ViewSaleDto _sale;
        private readonly TenantModel? _tenant;
        private readonly string _currency;
        private readonly string _invoiceNo;

        public InvoiceDocument(ViewSaleDto sale, TenantModel? tenant)
        {
            _sale = sale;
            _tenant = tenant;
            _currency = tenant?.CurrencyCode ?? "";
            _invoiceNo = sale.Id.ToString("N")[..8].ToUpper();
        }

        public DocumentMetadata GetMetadata() => new DocumentMetadata
        {
            Title = $"Invoice #{_invoiceNo}",
            Author = _tenant?.Name ?? "Invoice"
        };

        public DocumentSettings GetSettings() => DocumentSettings.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontFamily(InvoiceFontFamily).FontSize(10).FontColor("#1e293b"));

                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }

        void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                // ── Header ─────────────────────────────────────────
                col.Item().Row(row =>
                {
                    // Shop info (left)
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(_tenant?.Name ?? "Shop")
                            .Bold().FontSize(18).FontColor("#0f172a");

                        if (!string.IsNullOrEmpty(_tenant?.ContactPhone))
                            c.Item().PaddingTop(3)
                                .Text(_tenant.ContactPhone).FontColor("#64748b");

                        if (!string.IsNullOrEmpty(_tenant?.ContactEmail))
                            c.Item().Text(_tenant.ContactEmail).FontColor("#64748b");

                        if (!string.IsNullOrEmpty(_tenant?.Country))
                            c.Item().Text(_tenant.Country).FontColor("#64748b");
                    });

                    // Invoice label (right)
                    row.ConstantItem(160).AlignRight().Column(c =>
                    {
                        c.Item().Text("INVOICE")
                            .Bold().FontSize(26).FontColor("#6366f1");
                        c.Item().PaddingTop(4)
                            .Text($"#{_invoiceNo}").FontColor("#94a3b8").FontSize(11);
                    });
                });

                col.Item().PaddingVertical(12).LineHorizontal(1).LineColor("#e2e8f0");

                // ── Date + Customer ────────────────────────────────
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Date").FontSize(9).FontColor("#94a3b8")
                            .Bold().LetterSpacing(0.05f);
                        c.Item().PaddingTop(3)
                            .Text(_sale.SaleDate.ToString("dd MMMM yyyy"))
                            .FontSize(11).SemiBold();
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Bill To").FontSize(9).FontColor("#94a3b8")
                            .Bold().LetterSpacing(0.05f);
                        c.Item().PaddingTop(3)
                            .Text(_sale.CustomerName ?? "Walk-in Customer")
                            .FontSize(11).SemiBold();

                        if (!string.IsNullOrEmpty(_sale.CustomerPhone))
                            c.Item().Text(_sale.CustomerPhone).FontColor("#64748b");

                        if (!string.IsNullOrEmpty(_sale.CustomerAddress))
                            c.Item().Text(_sale.CustomerAddress).FontColor("#64748b");
                    });
                });

                col.Item().PaddingTop(24);

                // ── Items table ────────────────────────────────────
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(28);   // #
                        cols.RelativeColumn(4);     // Item
                        cols.RelativeColumn();      // Qty
                        cols.RelativeColumn();      // Unit Price
                        cols.RelativeColumn();      // Total
                    });

                    // Header row
                    static IContainer HeaderCell(IContainer c) =>
                        c.Background("#6366f1").Padding(8).DefaultTextStyle(
                            t => t.FontColor("#ffffff").Bold().FontSize(9));

                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("#");
                        h.Cell().Element(HeaderCell).Text("Item");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Unit Price");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Total");
                    });

                    // Data rows
                    var rowIndex = 1;
                    foreach (var item in _sale.Items)
                    {
                        var bg = rowIndex % 2 == 0 ? "#f8fafc" : "#ffffff";

                        IContainer DataCell(IContainer c) =>
                            c.Background(bg).Padding(7);

                        table.Cell().Element(DataCell).Text(rowIndex.ToString()).FontColor("#64748b");
                        table.Cell().Element(DataCell).Text(item.ProductVariantName);
                        table.Cell().Element(DataCell).AlignRight().Text(item.Quantity.ToString());
                        table.Cell().Element(DataCell).AlignRight()
                            .Text($"{item.UnitPrice:N0} {_currency}");
                        table.Cell().Element(DataCell).AlignRight()
                            .Text($"{item.LineTotal:N0} {_currency}").SemiBold();

                        rowIndex++;
                    }
                });

                // ── Totals ─────────────────────────────────────────
                var subtotal = _sale.Items.Sum(i => i.LineTotal) + _sale.Discount;

                col.Item().AlignRight().PaddingTop(16).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(110).Text("Subtotal").FontColor("#64748b");
                        r.ConstantItem(110).AlignRight()
                            .Text($"{subtotal:N0} {_currency}");
                    });

                    if (_sale.Discount > 0)
                    {
                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.ConstantItem(110).Text("Discount").FontColor("#ef4444");
                            r.ConstantItem(110).AlignRight()
                                .Text($"- {_sale.Discount:N0} {_currency}").FontColor("#ef4444");
                        });
                    }

                    c.Item().PaddingTop(8)
                        .BorderTop(1).BorderColor("#e2e8f0")
                        .PaddingTop(8)
                        .Row(r =>
                        {
                            r.ConstantItem(110).Text("Total").Bold().FontSize(13);
                            r.ConstantItem(110).AlignRight()
                                .Text($"{_sale.TotalAmount:N0} {_currency}")
                                .Bold().FontSize(13).FontColor("#6366f1");
                        });
                });
            });
        }

        void ComposeFooter(IContainer container)
        {
            container.PaddingTop(8).BorderTop(1).BorderColor("#e2e8f0")
                .AlignCenter()
                .Text("Thank you for your business!")
                .FontColor("#94a3b8").FontSize(9);
        }
    }
}
