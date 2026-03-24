namespace Kinnect.Models;

public class PagedItems<T> // TODO: Remove in place of using existing IPageCollection from Extenso
{
    public IEnumerable<T> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}