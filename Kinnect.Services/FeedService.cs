using Ardalis.Result;
using Kinnect.Models;
using Kinnect.Services.Abstractions;

namespace Kinnect.Services;

public class FeedService(IRepository<Post> postRepository, IRepository<Photo> photoRepository, IRepository<Video> videoRepository) : IFeedService
{
    public async Task<Result<IEnumerable<FeedItemDto>>> GetFeedAsync(int page = 1, int pageSize = 20)
    {
        var posts = await postRepository.FindAsync(new SearchOptions<Post>
        {
            Include = q => q.Include(p => p.Author)
        });

        var photos = await photoRepository.FindAsync(new SearchOptions<Photo>
        {
            Include = q => q.Include(p => p.UploadedBy)
        });

        var videos = await videoRepository.FindAsync(new SearchOptions<Video>
        {
            Include = q => q.Include(v => v.UploadedBy)
        });

        var feedItems = new List<FeedItemDto>();

        feedItems.AddRange(posts.Select(p => new FeedItemDto
        {
            Type = "post",
            Id = p.Id,
            AuthorName = $"{p.Author.GivenNames} {p.Author.FamilyName}",
            AuthorProfileImage = p.Author.ProfileImagePath,
            AuthorPersonId = p.AuthorPersonId,
            Content = p.Content,
            CreatedAtUtc = p.CreatedAtUtc
        }));

        feedItems.AddRange(photos.Select(p => new FeedItemDto
        {
            Type = "photo",
            Id = p.Id,
            AuthorName = $"{p.UploadedBy.GivenNames} {p.UploadedBy.FamilyName}",
            AuthorProfileImage = p.UploadedBy.ProfileImagePath,
            AuthorPersonId = p.UploadedByPersonId,
            Title = p.Title,
            ThumbnailPath = p.ThumbnailPath ?? p.FilePath,
            CreatedAtUtc = p.CreatedAtUtc
        }));

        feedItems.AddRange(videos.Select(v => new FeedItemDto
        {
            Type = "video",
            Id = v.Id,
            AuthorName = $"{v.UploadedBy.GivenNames} {v.UploadedBy.FamilyName}",
            AuthorProfileImage = v.UploadedBy.ProfileImagePath,
            AuthorPersonId = v.UploadedByPersonId,
            Title = v.Title,
            ThumbnailPath = v.ThumbnailPath,
            CreatedAtUtc = v.CreatedAtUtc
        }));

        var result = feedItems
            .OrderByDescending(f => f.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        return Result.Success(result);
    }
}