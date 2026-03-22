using Ardalis.Result;
using Kinnect.Models;
using Kinnect.Services.Abstractions;

namespace Kinnect.Services;

public class PostService(IRepository<Post> postRepository, IRepository<Person> personRepository) : IPostService
{
    public async Task<Result<IEnumerable<PostDto>>> GetRecentAsync(int page = 1, int pageSize = 20)
    {
        var posts = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Include = q => q.Include(p => p.Author)
        });

        var result = posts
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDto);

        return Result.Success(result);
    }

    public async Task<Result<IEnumerable<PostDto>>> GetByPersonAsync(int personId)
    {
        var posts = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Query = x => x.AuthorPersonId == personId,
            Include = q => q.Include(p => p.Author)
        });

        return Result.Success(posts.OrderByDescending(p => p.CreatedAtUtc).Select(MapToDto));
    }

    public async Task<Result<PagedItems<PostDto>>> GetByPersonPagedAsync(int personId, int page = 1, int pageSize = 10)
    {
        var posts = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Query = x => x.AuthorPersonId == personId,
            Include = q => q.Include(p => p.Author)
        });

        var ordered = posts.OrderByDescending(p => p.CreatedAtUtc).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(MapToDto);

        return Result.Success(new PagedItems<PostDto>
        {
            Items = items,
            TotalCount = ordered.Count,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<PostDto>> GetByIdAsync(int id)
    {
        var posts = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Query = x => x.Id == id,
            Include = q => q.Include(p => p.Author)
        });
        var post = posts.FirstOrDefault();

        if (post is null)
            return Result.NotFound("Post not found.");

        return Result.Success(MapToDto(post));
    }

    public async Task<Result<PostDto>> CreateAsync(PostCreateRequest request, int authorPersonId)
    {
        var author = await personRepository.FindOneAsync(authorPersonId);
        if (author is null)
            return Result.NotFound("Author person not found.");

        var post = new Post
        {
            AuthorPersonId = authorPersonId,
            Content = request.Content,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await postRepository.InsertAsync(post);

        post.Author = author;
        return Result.Success(MapToDto(post));
    }

    public async Task<Result<PostDto>> UpdateAsync(int id, PostEditRequest request, string currentUserId)
    {
        var posts = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Query = x => x.Id == id,
            Include = q => q.Include(p => p.Author)
        });
        var post = posts.FirstOrDefault();

        if (post is null)
            return Result.NotFound("Post not found.");

        if (post.Author.UserId != currentUserId)
            return Result.Forbidden();

        post.Content = request.Content;
        post.UpdatedAtUtc = DateTime.UtcNow;
        await postRepository.UpdateAsync(post);

        return Result.Success(MapToDto(post));
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId)
    {
        var posts = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Query = x => x.Id == id,
            Include = q => q.Include(p => p.Author)
        });
        var post = posts.FirstOrDefault();

        if (post is null)
            return Result.NotFound("Post not found.");

        if (post.Author.UserId != currentUserId)
            return Result.Forbidden();

        await postRepository.DeleteAsync(post);
        return Result.Success();
    }

    private static PostDto MapToDto(Post p) => new()
    {
        Id = p.Id,
        AuthorPersonId = p.AuthorPersonId,
        AuthorName = $"{p.Author.GivenNames} {p.Author.FamilyName}",
        AuthorProfileImage = p.Author.ProfileImagePath,
        Content = p.Content,
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc
    };
}