namespace Kinnect.Models;

public class DocumentDto
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string? Description { get; set; }

    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }

    public int UploadedByPersonId { get; set; }

    public string UploadedByName { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public List<string> Tags { get; set; } = [];
}