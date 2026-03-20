using Ardalis.Result;
using Kinnect.Models;
using Kinnect.Services.Abstractions;

namespace Kinnect.Services;

public class VideoService(IRepository<Video> videoRepository, IRepository<Tag> tagRepository, IRepository<VideoTag> videoTagRepository) : IVideoService
{
    public async Task<Result<IEnumerable<VideoDto>>> GetByPersonAsync(int personId)
    {
        var videos = await videoRepository.FindAsync(new SearchOptions<Video>
        {
            Query = x => x.UploadedByPersonId == personId,
            Include = q => q.Include(v => v.UploadedBy).Include(v => v.VideoTags).ThenInclude(vt => vt.Tag)
        });

        return Result.Success(videos.OrderByDescending(v => v.CreatedAtUtc).Select(MapToDto));
    }

    public async Task<Result<VideoDto>> GetByIdAsync(int id)
    {
        var videos = await videoRepository.FindAsync(new SearchOptions<Video>
        {
            Query = x => x.Id == id,
            Include = q => q.Include(v => v.UploadedBy).Include(v => v.VideoTags).ThenInclude(vt => vt.Tag)
        });
        var video = videos.FirstOrDefault();

        if (video is null)
            return Result.NotFound("Video not found.");

        return Result.Success(MapToDto(video));
    }

    public async Task<Result<VideoDto>> CreateAsync(string title, string? description, string filePath, string? thumbnailPath, TimeSpan? duration, int uploadedByPersonId, List<string>? tags)
    {
        var video = new Video
        {
            Title = title,
            Description = description,
            FilePath = filePath,
            ThumbnailPath = thumbnailPath,
            Duration = duration,
            UploadedByPersonId = uploadedByPersonId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await videoRepository.InsertAsync(video);

        if (tags is { Count: > 0 })
        {
            await SyncTagsAsync(video.Id, tags);
        }

        return Result.Success(new VideoDto
        {
            Id = video.Id,
            Title = video.Title,
            FilePath = video.FilePath,
            ThumbnailPath = video.ThumbnailPath,
            Description = video.Description,
            Duration = video.Duration,
            UploadedByPersonId = video.UploadedByPersonId,
            CreatedAtUtc = video.CreatedAtUtc,
            Tags = tags ?? []
        });
    }

    public async Task<Result> UpdateTagsAsync(int id, List<string> tags)
    {
        var video = await videoRepository.FindOneAsync(id);
        if (video is null)
            return Result.NotFound("Video not found.");

        await SyncTagsAsync(id, tags);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId)
    {
        var video = await videoRepository.FindOneAsync(id);
        if (video is null)
            return Result.NotFound("Video not found.");

        await videoRepository.DeleteAsync(video);
        return Result.Success();
    }

    private async Task SyncTagsAsync(int videoId, List<string> tagNames)
    {
        var existing = await videoTagRepository.FindAsync(new SearchOptions<VideoTag>
        {
            Query = x => x.VideoId == videoId
        });

        foreach (var vt in existing)
        {
            await videoTagRepository.DeleteAsync(vt);
        }

        foreach (string tagName in tagNames.Distinct())
        {
            var tags = await tagRepository.FindAsync(new SearchOptions<Tag>
            {
                Query = x => x.Name == tagName
            });
            var tag = tags.FirstOrDefault();

            if (tag is null)
            {
                tag = new Tag { Name = tagName };
                await tagRepository.InsertAsync(tag);
            }

            await videoTagRepository.InsertAsync(new VideoTag { VideoId = videoId, TagId = tag.Id });
        }
    }

    private static VideoDto MapToDto(Video v) => new()
    {
        Id = v.Id,
        Title = v.Title,
        FilePath = v.FilePath,
        ThumbnailPath = v.ThumbnailPath,
        Description = v.Description,
        Duration = v.Duration,
        UploadedByPersonId = v.UploadedByPersonId,
        UploadedByName = $"{v.UploadedBy.GivenNames} {v.UploadedBy.FamilyName}",
        CreatedAtUtc = v.CreatedAtUtc,
        Tags = v.VideoTags.Select(vt => vt.Tag.Name).ToList()
    };
}