namespace Kinnect.Models;

public class PagedItems<T>
{
    public IEnumerable<T> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

public class PostDto
{
    public int Id { get; set; }

    public int AuthorPersonId { get; set; }

    public string AuthorName { get; set; } = null!;

    public string? AuthorProfileImage { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public class PostCreateRequest
{
    public string Content { get; set; } = null!;
}

public class PostEditRequest
{
    public string Content { get; set; } = null!;
}