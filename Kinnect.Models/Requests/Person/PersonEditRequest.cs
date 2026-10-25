namespace Kinnect.Models.Requests;

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

    /// <summary>
    /// Named links for the profile. Null leaves the stored list alone, so an update that
    /// does not mention links does not wipe them.
    /// </summary>
    public List<PersonLinkInput>? Links { get; set; }

    /// <summary>
    /// Contact details (email, mobile, Facebook and so on). Null leaves the stored list alone, like <see cref="Links"/>.
    /// </summary>
    public List<ContactInput>? Contacts { get; set; }
}