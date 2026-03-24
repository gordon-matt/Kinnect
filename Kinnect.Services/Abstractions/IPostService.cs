namespace Kinnect.Services.Abstractions;

public interface IPostService
{
    Task<Result<PostDto>> CreateAsync(PostCreateRequest request, int authorPersonId);

    Task<Result> DeleteAsync(int id, string currentUserId);

    Task<Result<PostDto>> GetByIdAsync(int id);

    Task<Result<IEnumerable<PostDto>>> GetByPersonAsync(int personId);

    Task<Result<PagedItems<PostDto>>> GetByPersonPagedAsync(int personId, int page = 1, int pageSize = 10);

    Task<Result<IEnumerable<PostDto>>> GetRecentAsync(int page = 1, int pageSize = 20);

    Task<Result<PostDto>> UpdateAsync(int id, PostEditRequest request, string currentUserId);
}