namespace Kinnect.Services;

public class PhotoService(
    IRepository<Photo> photoRepository,
    IRepository<Tag> tagRepository,
    IRepository<PhotoTag> photoTagRepository,
    IRepository<PersonPhoto> personPhotoRepository) : IPhotoService
{
    public async Task<Result<PhotoDto>> CreateAsync(
        string title,
        string? description,
        string filePath,
        string? thumbnailPath,
        int uploadedByPersonId,
        List<string>? tags,
        short? yearTaken = null,
        byte? monthTaken = null,
        byte? dayTaken = null,
        int? folderId = null,
        double? latitude = null,
        double? longitude = null)
    {
        var photo = await photoRepository.InsertAsync(new Photo
        {
            Title = title,
            Description = description,
            FilePath = filePath,
            ThumbnailPath = thumbnailPath,
            UploadedByPersonId = uploadedByPersonId,
            CreatedAtUtc = DateTime.UtcNow,
            YearTaken = yearTaken,
            MonthTaken = monthTaken,
            DayTaken = dayTaken,
            FolderId = folderId,
            Latitude = latitude,
            Longitude = longitude,
            LatLongAcquiredFromExif = latitude != null && longitude != null
        });

        if (tags is { Count: > 0 })
        {
            await SyncTagsAsync(photo.Id, tags);
        }

        var created = await GetByIdAsync(photo.Id);
        return created.IsSuccess ? Result.Success(created.Value) : Result.Error("Failed to load created photo.");
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId, bool isAdmin)
    {
        var photo = await photoRepository.FindOneAsync(new SearchOptions<Photo>
        {
            Query = x => x.Id == id,
            Include = q => q.Include(p => p.UploadedBy)
        });

        if (photo is null)
        {
            return Result.NotFound("Photo not found.");
        }

        if (!CanEditPhoto(photo.UploadedBy, currentUserId, isAdmin))
        {
            return Result.Forbidden();
        }

        await photoRepository.DeleteAsync(photo);
        return Result.Success();
    }

    public async Task<Result<PhotoDto>> GetByIdAsync(int id)
    {
        var photo = await photoRepository.FindOneAsync(new SearchOptions<Photo>
        {
            Query = x => x.Id == id,
            Include = q => q
                .Include(p => p.UploadedBy)
                .Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.PersonPhotos).ThenInclude(pp => pp.Person)
        });

        return photo is null ? (Result<PhotoDto>)Result.NotFound("Photo not found.") : Result.Success(photo.ToDto());
    }

    public async Task<Result<IEnumerable<PhotoDto>>> GetByPersonAsync(int personId)
    {
        var ownedPhotos = await photoRepository.FindAsync(new SearchOptions<Photo>
        {
            Query = x => x.UploadedByPersonId == personId,
            Include = q => q
                .Include(p => p.UploadedBy)
                .Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.PersonPhotos).ThenInclude(pp => pp.Person)
        });

        var taggedPhotoIds = (await personPhotoRepository
            .FindAsync(new SearchOptions<PersonPhoto>
            {
                Query = x => x.PersonId == personId
            }, x => x.PhotoId))
            .Distinct()
            .ToList();

        IEnumerable<Photo> taggedPhotos = [];
        if (taggedPhotoIds.Count > 0)
        {
            taggedPhotos = await photoRepository.FindAsync(new SearchOptions<Photo>
            {
                Query = x => taggedPhotoIds.Contains(x.Id),
                Include = q => q
                    .Include(p => p.UploadedBy)
                    .Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
                    .Include(p => p.PersonPhotos).ThenInclude(pp => pp.Person)
            });
        }

        var combined = ownedPhotos
            .Concat(taggedPhotos)
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => p.ToDto());

        return Result.Success(combined);
    }

    public async Task<Result> SaveAnnotationsAsync(int photoId, string? annotationsJson, string currentUserId, bool isAdmin)
    {
        var photo = await photoRepository.FindOneAsync(new SearchOptions<Photo>
        {
            Query = x => x.Id == photoId,
            Include = q => q.Include(p => p.UploadedBy)
        });

        if (photo is null)
        {
            return Result.NotFound("Photo not found.");
        }

        if (!CanEditPhoto(photo.UploadedBy, currentUserId, isAdmin))
        {
            return Result.Forbidden();
        }

        photo.AnnotationsJson = annotationsJson;
        await photoRepository.UpdateAsync(photo);
        return Result.Success();
    }

    public async Task<Result> TagPersonAsync(int photoId, int personId, string currentUserId, bool isAdmin)
    {
        var photo = await photoRepository.FindOneAsync(new SearchOptions<Photo>
        {
            Query = x => x.Id == photoId,
            Include = q => q.Include(p => p.UploadedBy)
        });

        if (photo is null)
        {
            return Result.NotFound("Photo not found.");
        }

        if (!CanEditPhoto(photo.UploadedBy, currentUserId, isAdmin))
        {
            return Result.Forbidden();
        }

        bool exists = await personPhotoRepository.ExistsAsync(x => x.PhotoId == photoId && x.PersonId == personId);
        if (!exists)
        {
            await personPhotoRepository.InsertAsync(new PersonPhoto { PhotoId = photoId, PersonId = personId });
        }

        return Result.Success();
    }

    public async Task<Result> UntagPersonAsync(int photoId, int personId, string currentUserId, bool isAdmin)
    {
        var photo = await photoRepository.FindOneAsync(new SearchOptions<Photo>
        {
            Query = x => x.Id == photoId,
            Include = q => q.Include(p => p.UploadedBy)
        });

        if (photo is null)
        {
            return Result.NotFound("Photo not found.");
        }

        if (!CanEditPhoto(photo.UploadedBy, currentUserId, isAdmin))
        {
            return Result.Forbidden();
        }

        await personPhotoRepository.DeleteAsync(x => x.PhotoId == photoId && x.PersonId == personId);

        return Result.Success();
    }

    public async Task<Result<PhotoDto>> UpdateAsync(int id, PhotoUpdateRequest request, string currentUserId, bool isAdmin)
    {
        var photo = await photoRepository.FindOneAsync(new SearchOptions<Photo>
        {
            Query = x => x.Id == id,
            Include = q => q
                .Include(p => p.UploadedBy)
                .Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.PersonPhotos).ThenInclude(pp => pp.Person)
        });

        if (photo is null)
        {
            return Result.NotFound("Photo not found.");
        }

        if (!CanEditPhoto(photo.UploadedBy, currentUserId, isAdmin))
        {
            return Result.Forbidden();
        }

        // GPS coordinates are extracted from EXIF at upload time (best-effort).
        // We only allow location edits when the existing coordinates were NOT acquired from EXIF.
        // Enforced server-side to prevent tampering with the UI.
        //
        // Note: because JSON model binding converts missing values to null, we treat
        // "GPS edits not provided" as "do not touch existing GPS coordinates".
        bool photoHasExifGps = photo.LatLongAcquiredFromExif && photo.Latitude != null && photo.Longitude != null;
        bool requestHasGps = request.Latitude != null || request.Longitude != null;

        if (photoHasExifGps && requestHasGps)
        {
            // Compare with a tolerance to avoid accidental mismatches due to JSON round-tripping.
            bool gpsRequestedToChange =
                request.Latitude == null ||
                request.Longitude == null ||
                Math.Abs(request.Latitude.Value - photo.Latitude!.Value) > 1e-7 ||
                Math.Abs(request.Longitude.Value - photo.Longitude!.Value) > 1e-7;
            if (gpsRequestedToChange)
            {
                return Result.Forbidden("This photo has GPS coordinates from EXIF and location edits are not allowed.");
            }
        }

        photo.Title = request.Title;
        photo.Description = request.Description;
        photo.YearTaken = request.YearTaken;
        photo.MonthTaken = request.MonthTaken;
        photo.DayTaken = request.DayTaken;
        photo.FolderId = request.FolderId;
        if (requestHasGps)
        {
            photo.Latitude = request.Latitude;
            photo.Longitude = request.Longitude;
            // Since the user supplied/modified GPS coordinates, the source is no longer EXIF.
            // (When EXIF-derived coords are locked, we either forbid coordinate changes
            // or allow the update only when coords are effectively unchanged.)
            if (!photoHasExifGps)
            {
                photo.LatLongAcquiredFromExif = false;
            }
        }

        await photoRepository.UpdateAsync(photo);

        if (request.Tags is not null)
        {
            await SyncTagsAsync(id, request.Tags);
        }

        if (request.PersonIds is not null)
        {
            await SyncPersonTagsAsync(id, request.PersonIds);
        }

        return await GetByIdAsync(id);
    }

    public async Task<Result> UpdateTagsAsync(int id, List<string> tags)
    {
        var photo = await photoRepository.FindOneAsync(id);
        if (photo is null)
        {
            return Result.NotFound("Photo not found.");
        }

        await SyncTagsAsync(id, tags);
        return Result.Success();
    }

    private static bool CanEditPhoto(Person uploadedBy, string currentUserId, bool isAdmin)
        => isAdmin || uploadedBy.UserId is null || uploadedBy.UserId == currentUserId;

    private async Task SyncPersonTagsAsync(int photoId, List<int> personIds)
    {
        await personPhotoRepository.DeleteAsync(x => x.PhotoId == photoId);

        var toInsert = personIds.Distinct().Select(x => new PersonPhoto { PhotoId = photoId, PersonId = x });
        await personPhotoRepository.InsertAsync(toInsert);
    }

    private async Task SyncTagsAsync(int photoId, List<string> tagNames)
    {
        await photoTagRepository.DeleteAsync(x => x.PhotoId == photoId);

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

        var photoTagsToInsert = newTags
            .Select(t => new PhotoTag { PhotoId = photoId, TagId = t.Id });

        await tagRepository.InsertAsync(newTags);
        await photoTagRepository.InsertAsync(photoTagsToInsert);
    }
}