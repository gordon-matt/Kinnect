using Extenso.Collections.Generic;

namespace Kinnect.Services;

public class PostService(IMappedRepository<PostDto, Post> postRepository, IRepository<Person> personRepository) : IPostService
{
    public async Task<Result<PostDto>> CreateAsync(PostCreateRequest request, int authorPersonId)
    {
        var author = await personRepository.FindOneAsync(authorPersonId);
        if (author is null)
        {
            return Result.NotFound("Author person not found.");
        }

        var now = DateTime.UtcNow;
        var inserted = await postRepository.InsertAsync(new PostDto
        {
            AuthorPersonId = authorPersonId,
            Content = request.Content,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            AuthorName = $"{author.GivenNames} {author.FamilyName}",
            AuthorProfileImage = author.ProfileImagePath,
            AuthorUserId = author.UserId
        });

        return Result.Success(inserted);
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId)
    {
        var post = await postRepository.FindOneAsync(new SearchOptions<Post>
        {
            Query = x => x.Id == id,
            Include = query => query.Include(p => p.Author)
        });

        if (post is null)
        {
            return Result.NotFound("Post not found.");
        }

        if (post.AuthorUserId != currentUserId)
        {
            return Result.Forbidden();
        }

        await postRepository.DeleteAsync(post);
        return Result.Success();
    }

    public async Task<Result<PostDto>> GetByIdAsync(int id)
    {
        var post = await postRepository.FindOneAsync(new SearchOptions<Post>
        {
            Query = x => x.Id == id,
            Include = query => query.Include(p => p.Author)
        });

        return post is null ? (Result<PostDto>)Result.NotFound("Post not found.") : Result.Success(post);
    }

    public async Task<Result<IEnumerable<PostDto>>> GetByPersonAsync(int personId)
    {
        var posts = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Query = x => x.AuthorPersonId == personId,
            Include = query => query.Include(p => p.Author),
            OrderBy = query => query.OrderByDescending(p => p.CreatedAtUtc)
        });

        return Result.Success(posts as IEnumerable<PostDto>);
    }

    public async Task<Result<IPagedCollection<PostDto>>> GetByPersonPagedAsync(int personId, int page = 1, int pageSize = 10)
    {
        var paged = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Query = x => x.AuthorPersonId == personId,
            Include = query => query.Include(p => p.Author),
            OrderBy = query => query.OrderByDescending(p => p.CreatedAtUtc),
            PageNumber = page,
            PageSize = pageSize
        });

        return Result.Success(paged);
    }

    public async Task<Result<IEnumerable<PostDto>>> GetRecentAsync(int page = 1, int pageSize = 20)
    {
        var posts = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Include = query => query.Include(p => p.Author),
            OrderBy = query => query.OrderByDescending(p => p.CreatedAtUtc),
            PageNumber = page,
            PageSize = pageSize
        });

        return Result.Success(posts as IEnumerable<PostDto>);
    }

    public async Task<Result<PostDto>> UpdateAsync(int id, PostEditRequest request, string currentUserId)
    {
        var post = await postRepository.FindOneAsync(new SearchOptions<Post>
        {
            Query = x => x.Id == id,
            Include = query => query.Include(p => p.Author)
        });

        if (post is null)
        {
            return Result.NotFound("Post not found.");
        }

        if (post.AuthorUserId != currentUserId)
        {
            return Result.Forbidden();
        }

        post.Content = request.Content;
        post.UpdatedAtUtc = DateTime.UtcNow;
        var updated = await postRepository.UpdateAsync(post);

        return Result.Success(updated);
    }
}