namespace Kinnect.Models;

/// <summary>
/// Stable JSON shape for paged API responses (items + totalCount). Use at HTTP boundaries;
/// services may still return Extenso <c>IPagedCollection&lt;T&gt;</c> internally.
/// </summary>
public class PagedApiResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}