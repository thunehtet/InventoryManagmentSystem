namespace ClothInventoryApp.Models
{
    public class PaginationViewModel
    {
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string Action { get; init; } = "Index";
        public string? Controller { get; init; }

        // Extra route values forwarded to page links (search, filter, planId, etc.)
        public Dictionary<string, string?> Extra { get; init; } = new();

        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
        public int From => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
        public int To => Math.Min(Page * PageSize, TotalCount);
        public bool HasPrev => Page > 1;
        public bool HasNext => Page < TotalPages;

        public static readonly int[] AllowedSizes = [10, 50, 100];
        public static int Clamp(int size) => AllowedSizes.Contains(size) ? size : 10;
    }
}
