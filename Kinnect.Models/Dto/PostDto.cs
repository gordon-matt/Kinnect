using System.Text.Json.Serialization;

namespace Kinnect.Models.Dto;

public class PostDto
{
    public int Id { get; set; }

    public int AuthorPersonId { get; set; }

    /// <summary>ASP.NET Identity user id of the author; populated for permission checks, not exposed in JSON.</summary>
    [JsonIgnore]
    public string? AuthorUserId { get; set; }

    public string AuthorName { get; set; } = null!;

    public string? AuthorProfileImage { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}