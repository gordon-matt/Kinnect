using Kinnect.Models.FamilyTree;

namespace Kinnect.Services.Abstractions;

public interface IGedcomService
{
    /// <summary>Exports all people and relationships as a GEDCOM 5.5.1 string.</summary>
    Task<string> ExportAsync();

    /// <summary>
    /// Imports a GEDCOM, creating or updating Person records. The stream is either a plain .ged or a zip holding the
    /// .ged and its media files; with a zip the referenced images are imported as photos. The uploaded .ged is kept
    /// unchanged so a later export can merge into it.
    /// </summary>
    /// <param name="stream">The uploaded file.</param>
    /// <param name="fileName">Original file name, recorded with the stored copy.</param>
    Task<Result<GedcomImportResult>> ImportAsync(Stream stream, string? fileName = null);
}