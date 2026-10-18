namespace Kinnect.Models.Dto;

public enum MapPinKind
{
    Person,
    Event,
    Photo,
}

public class MapPinDto
{
    public int PersonId { get; set; }

    public MapPinKind Kind { get; set; } = MapPinKind.Person;

    public string FullName { get; set; } = null!;

    public string? ProfileImagePath { get; set; }

    /// <summary>Date line for an event or photo pin.</summary>
    public string? Detail { get; set; }

    public string? Place { get; set; }

    public string? Description { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}