namespace Kinnect.Models.FamilyTree;

public class GedcomImportResult
{
    public int PeopleImported { get; set; }

    public int PeopleUpdated { get; set; }

    public int RelationshipsImported { get; set; }

    public int EventsImported { get; set; }

    /// <summary>Photos created from the image files in the upload (one per distinct file).</summary>
    public int PhotosImported { get; set; }

    /// <summary>Documents created from the other files in the upload, such as PDFs.</summary>
    public int DocumentsImported { get; set; }

    /// <summary>Videos created from the video files in the upload.</summary>
    public int VideosImported { get; set; }

    /// <summary>Media references whose file was not in the upload, or that point to a folder rather than a file.</summary>
    public int MediaMissing { get; set; }

    /// <summary>Media Kinnect cannot store: web links, e-mail addresses, and files that are not images.</summary>
    public int MediaSkipped { get; set; }

    public List<string> Warnings { get; set; } = [];
}