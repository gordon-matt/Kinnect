namespace Kinnect.Models.Dto;

public sealed class CloseRelativeDto
{
    public int PersonId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Relationship { get; set; } = string.Empty;
}
