namespace Kinnect.Models.Dto;

public class PersonSpouseDetailDto
{
    public int SpousePersonId { get; set; }

    public string GivenNames { get; set; } = null!;

    public string FamilyName { get; set; } = null!;

    public string FullName => $"{GivenNames} {FamilyName}";

    public short? MarriageYear { get; set; }

    public byte? MarriageMonth { get; set; }

    public byte? MarriageDay { get; set; }

    public short? DivorceYear { get; set; }

    public byte? DivorceMonth { get; set; }

    public byte? DivorceDay { get; set; }

    public short? EngagementYear { get; set; }

    public byte? EngagementMonth { get; set; }

    public byte? EngagementDay { get; set; }

    public bool HasEngagement { get; set; }

    public bool HasMarriage { get; set; }

    public bool HasDivorce { get; set; }
}