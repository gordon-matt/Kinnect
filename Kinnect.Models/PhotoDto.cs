namespace Kinnect.Models;

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

    public List<string> Tags { get; set; } = [];

    public string? DateTakenDisplay
    {
        get
        {
            if (YearTaken == null) return null;
            if (MonthTaken != null && DayTaken != null)
                return $"{YearTaken:D4}-{MonthTaken:D2}-{DayTaken:D2}";
            if (MonthTaken != null)
                return $"{YearTaken:D4}-{MonthTaken:D2}";
            return YearTaken.Value.ToString("D4");
        }
    }
}