namespace Kinnect.Models.FamilyTree;

/// <summary>Which parts of the tree to write into an exported GEDCOM. Every part starts included.</summary>
public sealed class GedcomExportOptions
{
    public bool Names { get; set; } = true;

    public bool Notes { get; set; } = true;

    public bool Events { get; set; } = true;

    public bool Contacts { get; set; } = true;

    public bool Families { get; set; } = true;

    public bool Photos { get; set; } = true;

    public bool Videos { get; set; } = true;

    public bool Documents { get; set; } = true;

    /// <summary>GEDCOM 5.5, 5.5.1, 5.5.5 or 7.0, or GEDCOM X as <c>gedcomx-json</c> or <c>gedcomx-xml</c>.</summary>
    public string Version { get; set; } = "5.5.1";

    public bool IncludesMedia => Photos || Videos || Documents;

    public bool IsGedcomX => Version is "gedcomx-json" or "gedcomx-xml";

    /// <summary>The text export used by tests: every fact, and no media files.</summary>
    public static GedcomExportOptions WithoutMedia() => new()
    {
        Photos = false,
        Videos = false,
        Documents = false,
    };
}

/// <summary>How far an export has got. <see cref="Completed"/> and <see cref="Total"/> drive the progress bar.</summary>
public sealed record GedcomExportProgress(int Completed, int Total, string Label);

/// <summary>A generated .ged, .json or .xml, or a zip (.gdz or .zip) when media files are included.</summary>
public sealed record GedcomExportFile(string Path, string DownloadName, string ContentType);
