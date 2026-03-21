using Ardalis.Result;
using Kinnect.Data.Entities;
using Kinnect.Models;
using Kinnect.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Kinnect.Services;

public class PhotoService(IRepository<Photo> photoRepository, IRepository<Tag> tagRepository, IRepository<PhotoTag> photoTagRepository) : IPhotoService
{
    public async Task<Result<IEnumerable<PhotoDto>>> GetByPersonAsync(int personId)
    {
        var photos = await photoRepository.FindAsync(new SearchOptions<Photo>
        {
            Query = x => x.UploadedByPersonId == personId,
            Include = q => q.Include(p => p.UploadedBy).Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
        });

        return Result.Success(photos.OrderByDescending(p => p.CreatedAtUtc).Select(MapToDto));
    }

    public async Task<Result<PhotoDto>> GetByIdAsync(int id)
    {
        var photos = await photoRepository.FindAsync(new SearchOptions<Photo>
        {
            Query = x => x.Id == id,
            Include = q => q.Include(p => p.UploadedBy).Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
        });
        var photo = photos.FirstOrDefault();

        if (photo is null)
            return Result.NotFound("Photo not found.");

        return Result.Success(MapToDto(photo));
    }

    public async Task<Result<PhotoDto>> CreateAsync(string title, string? description, string filePath, string? thumbnailPath, int uploadedByPersonId, List<string>? tags, short? yearTaken = null, byte? monthTaken = null, byte? dayTaken = null)
    {
        var photo = new Photo
        {
            Title = title,
            Description = description,
            FilePath = filePath,
            ThumbnailPath = thumbnailPath,
            UploadedByPersonId = uploadedByPersonId,
            CreatedAtUtc = DateTime.UtcNow,
            YearTaken = yearTaken,
            MonthTaken = monthTaken,
            DayTaken = dayTaken
        };

        await photoRepository.InsertAsync(photo);

        if (tags is { Count: > 0 })
        {
            await SyncTagsAsync(photo.Id, tags);
        }

        var created = await GetByIdAsync(photo.Id);
        return created.IsSuccess ? Result.Success(created.Value) : Result.Error("Failed to load created photo.");
    }

    public async Task<Result<PhotoDto>> UpdateAsync(int id, PhotoUpdateRequest request, string currentUserId, bool isAdmin)
    {
        var photos = await photoRepository.FindAsync(new SearchOptions<Photo>
        {
            Query = x => x.Id == id,
            Include = q => q.Include(p => p.UploadedBy).Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
        });
        var photo = photos.FirstOrDefault();

        if (photo is null)
            return Result.NotFound("Photo not found.");

        if (!CanEditPhotoMetadata(photo.UploadedBy, currentUserId, isAdmin))
            return Result.Forbidden();

        photo.Title = request.Title;
        photo.Description = request.Description;
        photo.YearTaken = request.YearTaken;
        photo.MonthTaken = request.MonthTaken;
        photo.DayTaken = request.DayTaken;

        await photoRepository.UpdateAsync(photo);

        if (request.Tags is not null)
            await SyncTagsAsync(id, request.Tags);

        return await GetByIdAsync(id);
    }

    private static bool CanEditPhotoMetadata(Person uploadedBy, string currentUserId, bool isAdmin)
    {
        if (isAdmin)
            return true;
        if (uploadedBy.UserId == null)
            return true;
        return uploadedBy.UserId == currentUserId;
    }

    public async Task<Result> UpdateTagsAsync(int id, List<string> tags)
    {
        var photo = await photoRepository.FindOneAsync(id);
        if (photo is null)
            return Result.NotFound("Photo not found.");

        await SyncTagsAsync(id, tags);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId)
    {
        var photo = await photoRepository.FindOneAsync(id);
        if (photo is null)
            return Result.NotFound("Photo not found.");

        await photoRepository.DeleteAsync(photo);
        return Result.Success();
    }

    private async Task SyncTagsAsync(int photoId, List<string> tagNames)
    {
        var existing = await photoTagRepository.FindAsync(new SearchOptions<PhotoTag>
        {
            Query = x => x.PhotoId == photoId
        });

        foreach (var pt in existing)
        {
            await photoTagRepository.DeleteAsync(pt);
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

            await photoTagRepository.InsertAsync(new PhotoTag { PhotoId = photoId, TagId = tag.Id });
        }
    }

    private static PhotoDto MapToDto(Photo p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        FilePath = p.FilePath,
        ThumbnailPath = p.ThumbnailPath,
        Description = p.Description,
        UploadedByPersonId = p.UploadedByPersonId,
        UploadedByName = $"{p.UploadedBy.GivenNames} {p.UploadedBy.FamilyName}",
        CreatedAtUtc = p.CreatedAtUtc,
        YearTaken = p.YearTaken,
        MonthTaken = p.MonthTaken,
        DayTaken = p.DayTaken,
        Tags = p.PhotoTags.Select(pt => pt.Tag.Name).ToList()
    };
}