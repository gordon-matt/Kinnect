namespace Kinnect.Models;

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
}

public class PersonSpouseUpdateRequest
{
    public short? MarriageYear { get; set; }

    public byte? MarriageMonth { get; set; }

    public byte? MarriageDay { get; set; }

    public short? DivorceYear { get; set; }

    public byte? DivorceMonth { get; set; }

    public byte? DivorceDay { get; set; }
}
