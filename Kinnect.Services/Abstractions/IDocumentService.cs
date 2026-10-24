namespace Kinnect.Services.Abstractions;

public interface IDocumentService
{
    Task<Result<DocumentDto>> CreateAsync(
        string title,
        string? description,
        string filePath,
        string contentType,
        long fileSize,
        int uploadedByPersonId,
        List<string>? tags);

    Task<Result> DeleteAsync(int id, string currentUserId, bool isAdmin);

    Task<Result<DocumentDto>> UpdateAsync(int id, DocumentUpdateRequest request, string currentUserId, bool isAdmin);

    Task<Result<DocumentDto>> GetByIdAsync(int id);

    Task<Result<IEnumerable<DocumentDto>>> GetByPersonAsync(int personId);

    Task<Result> UpdateTagsAsync(int id, List<string> tags);
}