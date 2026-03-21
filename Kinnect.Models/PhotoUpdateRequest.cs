namespace Kinnect.Models;

public class PhotoUpdateRequest
{
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public short? YearTaken { get; set; }

    public byte? MonthTaken { get; set; }

    public byte? DayTaken { get; set; }

    public List<string>? Tags { get; set; }
}
