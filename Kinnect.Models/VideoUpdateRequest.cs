namespace Kinnect.Models;

public class VideoUpdateRequest
{
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public List<string>? Tags { get; set; }

    public int? FolderId { get; set; }
}