namespace Kinnect.Services;

public class VideoService(
    IRepository<Video> videoRepository,
    IRepository<Tag> tagRepository,
    IRepository<VideoTag> videoTagRepository,
    IRepository<VideoEvent> videoEventRepository,
    IFileStorageService fileStorage,
    ILogger<VideoService> logger) : IVideoService
{
    public async Task<Result<VideoDto>> CreateAsync(
        string title,
        string? description,
        string filePath,
        string? thumbnailPath,
        TimeSpan? duration,
        int uploadedByPersonId,
        List<string>? tags,
        int? folderId = null,
        bool isProcessing = false)
    {
        var video = await videoRepository.InsertAsync(new Video
        {
            Title = title,
            Description = description,
            FilePath = filePath,
            ThumbnailPath = thumbnailPath,
            Duration = duration,
            UploadedByPersonId = uploadedByPersonId,
            CreatedAtUtc = DateTime.UtcNow,
            FolderId = folderId,
            IsProcessing = isProcessing
        });

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
            Tags = tags ?? [],
            FolderId = folderId,
            IsProcessing = isProcessing
        });
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId, bool isAdmin)
    {
        var video = await videoRepository.FindOneAsync(new SearchOptions<Video>
        {
            Query = x => x.Id == id,
            Include = q => q.Include(v => v.UploadedBy)
        });

        if (video is null)
        {
            return Result.NotFound("Video not found.");
        }

        if (!CanEditVideo(video.UploadedBy, currentUserId, isAdmin))
        {
            return Result.Forbidden();
        }

        if (!await fileStorage.DeleteFileAsync(video.FilePath))
        {
            logger.LogWarning("Left video {VideoId} in place because {Path} is still open", video.Id, video.FilePath);
            return Result.Error("The video file is still open in another program, so it could not be removed.");
        }

        if (!string.IsNullOrWhiteSpace(video.ThumbnailPath))
        {
            await fileStorage.DeleteFileAsync(video.ThumbnailPath);
        }

        await videoRepository.DeleteAsync(video);
        logger.LogInformation("Deleted video {VideoId} for user {UserId}", video.Id, currentUserId);
        return Result.Success();
    }

    public async Task<Result<VideoDto>> GetByIdAsync(int id)
    {
        var video = await videoRepository.FindOneAsync(new SearchOptions<Video>
        {
            Query = x => x.Id == id,
            Include = q => q
                .Include(v => v.UploadedBy)
                .Include(v => v.VideoTags).ThenInclude(vt => vt.Tag)
        });

        if (video is null)
        {
            return Result.NotFound("Video not found.");
        }

        var dto = video.ToDto();
        await AttachEventsAsync([dto]);
        return Result.Success(dto);
    }

    public async Task<Result<IEnumerable<VideoDto>>> GetByPersonAsync(int personId)
    {
        var videos = await videoRepository.FindAsync(new SearchOptions<Video>
        {
            Query = x => x.UploadedByPersonId == personId,
            Include = q => q
                .Include(v => v.UploadedBy)
                .Include(v => v.VideoTags).ThenInclude(vt => vt.Tag),
            OrderBy = query => query.OrderByDescending(v => v.CreatedAtUtc)
        });

        var list = videos.Select(v => v.ToDto()).ToList();
        await AttachEventsAsync(list);
        return Result.Success<IEnumerable<VideoDto>>(list);
    }

    public async Task<Result<VideoDto>> UpdateAsync(int id, VideoUpdateRequest request, string currentUserId, bool isAdmin)
    {
        var video = await videoRepository.FindOneAsync(new SearchOptions<Video>
        {
            Query = x => x.Id == id,
            Include = q => q
                .Include(v => v.UploadedBy)
                .Include(v => v.VideoTags).ThenInclude(vt => vt.Tag)
        });

        if (video is null)
        {
            return Result.NotFound("Video not found.");
        }

        if (!CanEditVideo(video.UploadedBy, currentUserId, isAdmin))
        {
            return Result.Forbidden();
        }

        video.Title = request.Title;
        video.Description = request.Description;
        video.FolderId = request.FolderId;
        await videoRepository.UpdateAsync(video);

        if (request.Tags is not null)
        {
            await SyncTagsAsync(id, request.Tags);
        }

        if (request.EventIds is not null)
        {
            await videoEventRepository.DeleteAsync(x => x.VideoId == id);
            var links = request.EventIds
                .Distinct()
                .Select(eventId => new VideoEvent { VideoId = id, PersonEventId = eventId })
                .ToList();
            if (links.Count > 0)
            {
                await videoEventRepository.InsertAsync(links);
            }
        }

        return await GetByIdAsync(id);
    }

    public async Task<Result> UpdateTagsAsync(int id, List<string> tags)
    {
        bool videoExists = await videoRepository.ExistsAsync(x => x.Id == id);
        if (!videoExists)
        {
            return Result.NotFound("Video not found.");
        }

        await SyncTagsAsync(id, tags);
        return Result.Success();
    }

    private async Task AttachEventsAsync(IReadOnlyList<VideoDto> videos)
    {
        if (videos.Count == 0)
        {
            return;
        }

        var ids = videos.Select(video => video.Id).ToList();
        var links = await videoEventRepository.FindAsync(new SearchOptions<VideoEvent>
        {
            Query = link => ids.Contains(link.VideoId)
        });

        foreach (var video in videos)
        {
            video.EventIds = links.Where(link => link.VideoId == video.Id).Select(link => link.PersonEventId).ToList();
        }
    }

    private static bool CanEditVideo(Person uploadedBy, string currentUserId, bool isAdmin) =>
        isAdmin || uploadedBy.UserId == null || uploadedBy.UserId == currentUserId;

    private async Task SyncTagsAsync(int videoId, List<string> tagNames)
    {
        await videoTagRepository.DeleteAsync(x => x.VideoId == videoId);

        var distinctTagNames = tagNames
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
            .Distinct()
            .ToList();

        var existingTags = await tagRepository.FindAsync(new SearchOptions<Tag>
        {
            Query = x => distinctTagNames.Contains(x.Name)
        });

        var newTags = distinctTagNames
            .Where(x => !existingTags.Any(t => t.Name == x))
            .Select(x => new Tag { Name = x })
            .ToList();

        var videoTagsToInsert = newTags
            .Select(t => new VideoTag { VideoId = videoId, TagId = t.Id });

        await tagRepository.InsertAsync(newTags);
        await videoTagRepository.InsertAsync(videoTagsToInsert);
    }
}