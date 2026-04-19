namespace ClothInventoryApp.Dto.Reports
{
    public class SalesReportViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Preset { get; set; } = "this_month";

        public int TotalCount { get; set; }
        public int TotalRevenue { get; set; }
        public int TotalDiscount { get; set; }
        public int TotalProfit { get; set; }

        public List<SalesReportDayRow> DailyRows { get; set; } = new();
        public List<SalesReportItem> Sales { get; set; } = new();
    }

    public class SalesReportDayRow
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public int Revenue { get; set; }
        public int Discount { get; set; }
        public int Profit { get; set; }
    }

    public class SalesReportItem
    {
        public Guid Id { get; set; }
        public DateTime SaleDate { get; set; }
        public string? CustomerName { get; set; }
        public int TotalAmount { get; set; }
        public int Discount { get; set; }
        public int TotalProfit { get; set; }
    }
}
