namespace Kinnect.Models;

public class MediaFolderDto
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int CreatedByPersonId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class CreateMediaFolderRequest
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}
