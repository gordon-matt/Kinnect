namespace Kinnect.Models;

public class PersonVersionDto
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public string VersionData { get; set; } = null!;

    public string? ChangedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}