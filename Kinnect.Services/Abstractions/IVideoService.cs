using Ardalis.Result;
using Kinnect.Models;

namespace Kinnect.Services.Abstractions;

public interface IVideoService
{
    Task<Result<IEnumerable<VideoDto>>> GetByPersonAsync(int personId);

    Task<Result<VideoDto>> GetByIdAsync(int id);

    Task<Result<VideoDto>> CreateAsync(string title, string? description, string filePath, string? thumbnailPath, TimeSpan? duration, int uploadedByPersonId, List<string>? tags, int? folderId = null);

    Task<Result<VideoDto>> UpdateAsync(int id, VideoUpdateRequest request, string currentUserId, bool isAdmin);

    Task<Result> UpdateTagsAsync(int id, List<string> tags);

    Task<Result> DeleteAsync(int id, string currentUserId);
}