namespace Kinnect.Models.Dto;

public class MediaFolderDto
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int CreatedByPersonId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}