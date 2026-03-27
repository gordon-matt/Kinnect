using Kinnect.Models.FamilyTree;

namespace Kinnect.Services.Abstractions;

public interface IGedcomService
{
    /// <summary>Exports all people and relationships as a GEDCOM 5.5.1 string.</summary>
    Task<string> ExportAsync();

    /// <summary>Imports a GEDCOM stream, creating or updating Person records.</summary>
    Task<Result<GedcomImportResult>> ImportAsync(Stream stream);
}