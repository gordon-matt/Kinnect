namespace Kinnect.Models.Dto;

public class PersonEventDto
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public string EventType { get; set; } = null!;

    public string EventTypeLabel => PersonEventType.GetLabel(EventType);

    public short? Year { get; set; }

    public byte? Month { get; set; }

    public byte? Day { get; set; }

    public string? Place { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Description { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? DateDisplay => Year == null ? null : Month != null && Day != null ? $"{Year:D4}-{Month:D2}-{Day:D2}" : Year.Value.ToString("D4");
}