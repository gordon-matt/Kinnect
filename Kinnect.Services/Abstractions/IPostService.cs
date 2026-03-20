using Ardalis.Result;
using Kinnect.Models;

namespace Kinnect.Services.Abstractions;

public interface IPostService
{
    Task<Result<IEnumerable<PostDto>>> GetRecentAsync(int page = 1, int pageSize = 20);

    Task<Result<IEnumerable<PostDto>>> GetByPersonAsync(int personId);

    Task<Result<PostDto>> GetByIdAsync(int id);

    Task<Result<PostDto>> CreateAsync(PostCreateRequest request, int authorPersonId);

    Task<Result<PostDto>> UpdateAsync(int id, PostEditRequest request, string currentUserId);

    Task<Result> DeleteAsync(int id, string currentUserId);
}