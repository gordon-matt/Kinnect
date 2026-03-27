namespace Kinnect.Models.Dto;

public class PersonDto
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string FamilyName { get; set; } = null!;

    public string GivenNames { get; set; } = null!;

    public string FullName => $"{GivenNames} {FamilyName}";

    public bool IsMale { get; set; }

    public string? Bio { get; set; }

    public string? ProfileImagePath { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? FatherId { get; set; }

    public int? MotherId { get; set; }

    public string? Occupation { get; set; }

    public string? Education { get; set; }

    public string? Religion { get; set; }

    public string? Note { get; set; }

    public string? GedcomId { get; set; }

    public bool IsDeceased { get; set; }

    public bool HasAccount => UserId != null;
}