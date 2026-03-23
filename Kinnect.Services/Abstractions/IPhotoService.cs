using Ardalis.Result;
using Kinnect.Models;

namespace Kinnect.Services.Abstractions;

public interface IPhotoService
{
    Task<Result<IEnumerable<PhotoDto>>> GetByPersonAsync(int personId);

    Task<Result<PhotoDto>> GetByIdAsync(int id);

    Task<Result<PhotoDto>> CreateAsync(string title, string? description, string filePath, string? thumbnailPath, int uploadedByPersonId, List<string>? tags, short? yearTaken = null, byte? monthTaken = null, byte? dayTaken = null);

    Task<Result<PhotoDto>> UpdateAsync(int id, PhotoUpdateRequest request, string currentUserId, bool isAdmin);

    Task<Result> UpdateTagsAsync(int id, List<string> tags);

    Task<Result> SaveAnnotationsAsync(int photoId, string? annotationsJson, string currentUserId, bool isAdmin);

    Task<Result> TagPersonAsync(int photoId, int personId, string currentUserId, bool isAdmin);

    Task<Result> UntagPersonAsync(int photoId, int personId, string currentUserId, bool isAdmin);

    Task<Result> DeleteAsync(int id, string currentUserId);
}
