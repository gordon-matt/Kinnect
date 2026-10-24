namespace Kinnect.Models.Requests;

public class PhotoUpdateRequest
{
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public short? YearTaken { get; set; }

    public byte? MonthTaken { get; set; }

    public byte? DayTaken { get; set; }

    public List<string>? Tags { get; set; }

    /// <summary>Person event ids linked to this photo. Null leaves the current links unchanged.</summary>
    public List<int>? EventIds { get; set; }

    /// <summary>Person IDs to tag in this photo (syncs PersonPhotos).</summary>
    public List<int>? PersonIds { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? FolderId { get; set; }
}