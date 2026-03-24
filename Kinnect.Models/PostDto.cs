namespace Kinnect.Models;

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