using Ardalis.Result;
using Kinnect.Models;

namespace Kinnect.Services.Abstractions;

public interface IGedcomService
{
    /// <summary>Imports a GEDCOM stream, creating or updating Person records.</summary>
    Task<Result<GedcomImportResult>> ImportAsync(Stream stream);

    /// <summary>Exports all people and relationships as a GEDCOM 5.5.1 string.</summary>
    Task<string> ExportAsync();
}
