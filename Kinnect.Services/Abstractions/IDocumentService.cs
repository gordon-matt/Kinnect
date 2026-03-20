using Ardalis.Result;
using Kinnect.Models;

namespace Kinnect.Services.Abstractions;

public interface IDocumentService
{
    Task<Result<IEnumerable<DocumentDto>>> GetByPersonAsync(int personId);

    Task<Result<DocumentDto>> GetByIdAsync(int id);

    Task<Result<DocumentDto>> CreateAsync(string title, string? description, string filePath, string contentType, long fileSize, int uploadedByPersonId, List<string>? tags);

    Task<Result> UpdateTagsAsync(int id, List<string> tags);

    Task<Result> DeleteAsync(int id, string currentUserId);
}