namespace Kinnect.Models.Dto;

public record TaggedPersonInfo(int PersonId, string Name);

public class PhotoDto
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string? ThumbnailPath { get; set; }

    public string? Description { get; set; }

    public int UploadedByPersonId { get; set; }

    public string UploadedByName { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public short? YearTaken { get; set; }

    public byte? MonthTaken { get; set; }

    public byte? DayTaken { get; set; }

    public string? AnnotationsJson { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>
    /// True when the current latitude/longitude were acquired from EXIF at upload time.
    /// Used to lock GPS editing for EXIF-derived coordinates.
    /// </summary>
    public bool LatLongAcquiredFromExif { get; set; }

    public int? FolderId { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<int> EventIds { get; set; } = [];

    public List<TaggedPersonInfo> TaggedPeople { get; set; } = [];

    public string? DateTakenDisplay => YearTaken == null
        ? null
        : MonthTaken != null && DayTaken != null
            ? $"{YearTaken:D4}-{MonthTaken:D2}-{DayTaken:D2}"
            : MonthTaken != null ? $"{YearTaken:D4}-{MonthTaken:D2}" : YearTaken.Value.ToString("D4");
}