using MyWebTree.Models;

namespace Kinnect.Models.Dto;

public class PersonDto
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string FamilyName { get; set; } = null!;

    public string GivenNames { get; set; } = null!;

    public bool IsFamilyNameFirst { get; set; }

    public string? NamePrefix { get; set; }

    public string? NameSuffix { get; set; }

    public string? Nickname { get; set; }

    /// <summary>Given and family names in this person's order, without capitalization.</summary>
    public string FullName => PersonName.Format(GivenNames, FamilyName, IsFamilyNameFirst, capitalizeFamilyName: false);

    /// <summary>Null when the sex is unknown.</summary>
    public bool? IsMale { get; set; }

    public string? ProfileImagePath { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Occupation { get; set; }

    public string? Education { get; set; }

    public string? Religion { get; set; }

    public string? Note { get; set; }

    public string? GedcomId { get; set; }

    public bool IsDeceased { get; set; }

    public List<PersonLinkDto> Links { get; set; } = [];

    public List<ContactDto> Contacts { get; set; } = [];

    public bool HasAccount => UserId != null;
}