namespace Kinnect.Models;

public class FeedItemDto
{
    public string Type { get; set; } = null!;

    public int Id { get; set; }

    public string AuthorName { get; set; } = null!;

    public string? AuthorProfileImage { get; set; }

    public int AuthorPersonId { get; set; }

    public string? Content { get; set; }

    public string? Title { get; set; }

    public string? ThumbnailPath { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}