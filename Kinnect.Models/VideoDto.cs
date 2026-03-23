namespace Kinnect.Models;

public class VideoDto
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string? ThumbnailPath { get; set; }

    public string? Description { get; set; }

    public TimeSpan? Duration { get; set; }

    public int UploadedByPersonId { get; set; }

    public string UploadedByName { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<int> EventIds { get; set; } = [];

    public int? FolderId { get; set; }
}