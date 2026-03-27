namespace Kinnect.Models.Dto;

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

    public string? FilePath { get; set; }

    public string? AnnotationsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}