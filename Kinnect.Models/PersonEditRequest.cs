namespace Kinnect.Models;

public class PersonEditRequest
{
    public string FamilyName { get; set; } = null!;

    public string GivenNames { get; set; } = null!;

    public bool IsMale { get; set; }

    public string? Bio { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? FatherId { get; set; }

    public int? MotherId { get; set; }

    public string? Occupation { get; set; }

    public string? Education { get; set; }

    public string? Religion { get; set; }

    public string? Note { get; set; }

    public bool IsDeceased { get; set; }
}