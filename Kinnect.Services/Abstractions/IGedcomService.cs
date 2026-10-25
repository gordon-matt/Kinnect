using Kinnect.Models.FamilyTree;

namespace Kinnect.Services.Abstractions;

public interface IGedcomService
{
    /// <summary>Exports the tree as GEDCOM text in <see cref="GedcomExportOptions.Version"/> (5.5.1 when called through <see cref="ExportAsync"/>).</summary>
    Task<string> ExportAsync();

    /// <summary>The version of the stored import, such as 5.5.1 or 7.0, or null when the tree was not imported.</summary>
    Task<string?> ImportedVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes a .ged, or a .gdz (GEDZIP) package when photos, videos or documents are included.</summary>
    Task<GedcomExportFile> ExportFileAsync(
        GedcomExportOptions options,
        IProgress<GedcomExportProgress>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a GEDCOM, creating or updating Person records. The stream is either a plain .ged or a zip holding the
    /// .ged and its media files; with a zip the referenced images are imported as photos. The uploaded .ged is kept
    /// unchanged so a later export can merge into it.
    /// </summary>
    /// <param name="stream">The uploaded file.</param>
    /// <param name="fileName">Original file name, recorded with the stored copy.</param>
    Task<Result<GedcomImportResult>> ImportAsync(Stream stream, string? fileName = null);
}