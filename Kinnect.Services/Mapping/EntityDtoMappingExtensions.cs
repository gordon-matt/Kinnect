using Kinnect.Data.Entities;
using Kinnect.Models;

namespace Kinnect.Services.Mapping;

public static class EntityDtoMappingExtensions
{
    #region Post

    public static PostDto ToDto(this Post p) => new()
    {
        Id = p.Id,
        AuthorPersonId = p.AuthorPersonId,
        AuthorName = p.Author is not null ? $"{p.Author.GivenNames} {p.Author.FamilyName}" : string.Empty,
        AuthorProfileImage = p.Author?.ProfileImagePath,
        AuthorUserId = p.Author?.UserId,
        Content = p.Content,
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc
    };

    public static Post ToEntity(this PostDto dto) => new()
    {
        Id = dto.Id,
        AuthorPersonId = dto.AuthorPersonId,
        Content = dto.Content,
        CreatedAtUtc = dto.CreatedAtUtc,
        UpdatedAtUtc = dto.UpdatedAtUtc
    };

    #endregion Post

    #region Person

    public static PersonDto ToDto(this Person p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        FamilyName = p.FamilyName,
        GivenNames = p.GivenNames,
        IsMale = p.IsMale,
        Bio = p.Bio,
        ProfileImagePath = p.ProfileImagePath,
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        FatherId = p.FatherId,
        MotherId = p.MotherId,
        Occupation = p.Occupation,
        Education = p.Education,
        Religion = p.Religion,
        Note = p.Note,
        GedcomId = p.GedcomId,
        IsDeceased = p.IsDeceased
    };

    public static Person ToEntity(this PersonDto dto) => new()
    {
        Id = dto.Id,
        UserId = dto.UserId,
        FamilyName = dto.FamilyName,
        GivenNames = dto.GivenNames,
        IsMale = dto.IsMale,
        Bio = dto.Bio,
        ProfileImagePath = dto.ProfileImagePath,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude,
        FatherId = dto.FatherId,
        MotherId = dto.MotherId,
        Occupation = dto.Occupation,
        Education = dto.Education,
        Religion = dto.Religion,
        Note = dto.Note,
        GedcomId = dto.GedcomId,
        IsDeceased = dto.IsDeceased
    };

    #endregion Person

    #region PersonEvent

    public static PersonEventDto ToDto(this PersonEvent e) => new()
    {
        Id = e.Id,
        PersonId = e.PersonId,
        EventType = e.EventType,
        Year = e.Year,
        Month = e.Month,
        Day = e.Day,
        Place = e.Place,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        Description = e.Description,
        Note = e.Note,
        CreatedAtUtc = e.CreatedAtUtc
    };

    public static PersonEvent ToEntity(this PersonEventDto dto) => new()
    {
        Id = dto.Id,
        PersonId = dto.PersonId,
        EventType = dto.EventType,
        Year = dto.Year,
        Month = dto.Month,
        Day = dto.Day,
        Place = dto.Place,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude,
        Description = dto.Description,
        Note = dto.Note,
        CreatedAtUtc = dto.CreatedAtUtc
    };

    #endregion PersonEvent

    #region Photo

    public static PhotoDto ToDto(this Photo p) => new()
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
        AnnotationsJson = p.AnnotationsJson,
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        FolderId = p.FolderId,
        Tags = p.PhotoTags.Select(pt => pt.Tag.Name).ToList(),
        TaggedPeople = p.PersonPhotos
            .Select(pp => new TaggedPersonInfo(pp.PersonId, $"{pp.Person.GivenNames} {pp.Person.FamilyName}"))
            .ToList()
    };

    public static Photo ToEntity(this PhotoDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        FilePath = dto.FilePath,
        ThumbnailPath = dto.ThumbnailPath,
        Description = dto.Description,
        UploadedByPersonId = dto.UploadedByPersonId,
        CreatedAtUtc = dto.CreatedAtUtc,
        YearTaken = dto.YearTaken,
        MonthTaken = dto.MonthTaken,
        DayTaken = dto.DayTaken,
        AnnotationsJson = dto.AnnotationsJson,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude,
        FolderId = dto.FolderId
    };

    #endregion Photo

    #region Video

    public static VideoDto ToDto(this Video v) => new()
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
        Tags = v.VideoTags.Select(vt => vt.Tag.Name).ToList(),
        FolderId = v.FolderId
    };

    public static Video ToEntity(this VideoDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        FilePath = dto.FilePath,
        ThumbnailPath = dto.ThumbnailPath,
        Description = dto.Description,
        Duration = dto.Duration,
        UploadedByPersonId = dto.UploadedByPersonId,
        CreatedAtUtc = dto.CreatedAtUtc,
        FolderId = dto.FolderId
    };

    #endregion Video

    #region Document

    public static DocumentDto ToDto(this Document d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        FilePath = d.FilePath,
        Description = d.Description,
        ContentType = d.ContentType,
        FileSize = d.FileSize,
        UploadedByPersonId = d.UploadedByPersonId,
        UploadedByName = $"{d.UploadedBy.GivenNames} {d.UploadedBy.FamilyName}",
        CreatedAtUtc = d.CreatedAtUtc,
        Tags = d.DocumentTags.Select(dt => dt.Tag.Name).ToList()
    };

    public static Document ToEntity(this DocumentDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        FilePath = dto.FilePath,
        Description = dto.Description,
        ContentType = dto.ContentType,
        FileSize = dto.FileSize,
        UploadedByPersonId = dto.UploadedByPersonId,
        CreatedAtUtc = dto.CreatedAtUtc
    };

    #endregion Document
}
