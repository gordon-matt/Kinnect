namespace Kinnect.Models;

public class MapPinDto
{
    public int PersonId { get; set; }

    public string FullName { get; set; } = null!;

    public string? ProfileImagePath { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}