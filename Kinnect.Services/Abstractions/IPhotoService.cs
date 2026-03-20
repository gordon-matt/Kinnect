using Ardalis.Result;
using Kinnect.Models;

namespace Kinnect.Services.Abstractions;

public interface IPhotoService
{
    Task<Result<IEnumerable<PhotoDto>>> GetByPersonAsync(int personId);
    Task<Result<PhotoDto>> GetByIdAsync(int id);
    Task<Result<PhotoDto>> CreateAsync(string title, string? description, string filePath, string? thumbnailPath, int uploadedByPersonId, List<string>? tags);
    Task<Result> UpdateTagsAsync(int id, List<string> tags);
    Task<Result> DeleteAsync(int id, string currentUserId);
}
