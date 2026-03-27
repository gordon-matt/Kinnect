namespace Kinnect.Models.Requests;

public class PersonEventRequest
{
    public string EventType { get; set; } = null!;

    public short? Year { get; set; }

    public byte? Month { get; set; }

    public byte? Day { get; set; }

    public string? Place { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Description { get; set; }

    public string? Note { get; set; }
}