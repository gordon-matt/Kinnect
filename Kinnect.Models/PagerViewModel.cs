namespace Kinnect.Models;

/// <summary>
/// View model for the reusable _Pager partial. All *Expr / *Fn properties are
/// KnockoutJS observable/function names that are embedded verbatim into data-bind
/// attributes, so the partial generates the correct client-side bindings.
/// </summary>
public record PagerViewModel(
    /// <summary>Accessible label for the &lt;nav&gt; element (e.g. "Videos pagination").</summary>
    string AriaLabel,

    /// <summary>KO observable: current page number (e.g. "videosCurrentPage").</summary>
    string CurrentPageExpr,

    /// <summary>KO observable: total page count (e.g. "videosTotalPages").</summary>
    string TotalPagesExpr,

    /// <summary>KO observable: total item count (e.g. "videosTotalCount").</summary>
    string TotalCountExpr,

    /// <summary>KO computed: array of visible page numbers (e.g. "videosPageNumbers").</summary>
    string PageNumbersExpr,

    /// <summary>KO function: go to previous page (e.g. "videosPreviousPage").</summary>
    string PreviousPageFn,

    /// <summary>KO function: go to next page (e.g. "videosNextPage").</summary>
    string NextPageFn,

    /// <summary>KO function: go to a specific page – called with the page number (e.g. "videosGoToPage").</summary>
    string GoToPageFn,

    /// <summary>Human-readable noun for items shown in the summary line (e.g. "videos").</summary>
    string ItemLabel = "items"
);