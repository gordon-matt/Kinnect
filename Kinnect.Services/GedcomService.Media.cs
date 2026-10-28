using System.Security.Cryptography;
using GedcomSharp.Model;
using Kinnect.Models.FamilyTree;
using GedcomTags = GedcomSharp.GedcomTags;

namespace Kinnect.Services;

public partial class GedcomService
{
    /// <summary>Formats the image pipeline can decode. PDFs, audio and video are not photos.</summary>
    private static readonly HashSet<string> imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp",
    };

    private static readonly HashSet<string> videoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v", ".mpeg", ".mpg", ".wmv", ".ogv",
    };

    /// <summary>Scanned originals can be large, but a single entry bigger than this is more likely a mistake.</summary>
    private const long MaxMediaBytes = 100L * 1024 * 1024;

    private const int MaxWarnings = 25;

    /// <summary>
    /// Keeps the uploaded .ged unchanged on disk. A later export patches Kinnect's edits into this file so tags
    /// Kinnect does not model (and their order and formatting) survive.
    /// </summary>
    private async Task StoreOriginalAsync(string gedcomPath, string fileName)
    {
        await using var original = File.OpenRead(gedcomPath);
        string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(original));
        original.Position = 0;

        string storagePath = await fileStorage.SaveFileAsync(original, "original", fileName, Constants.FileStorage.GedcomOriginals);

        await gedcomFileRepository.InsertAsync(new GedcomFile
        {
            FileName = Truncate(fileName, 500),
            StoragePath = storagePath,
            Sha256 = hash,
            SizeBytes = original.Length,
            ImportedAtUtc = DateTime.UtcNow
        });
    }

    private async Task ImportIndividualMediaAsync(
        IndividualRecord individual,
        Person person,
        List<(GedcomEvent Source, int EventId)> importedEvents,
        MediaImportContext context)
    {
        int[] owner = [person.Id];

        // Media attached directly to the person. The first one becomes the profile picture.
        var own = await ImportMediaAsync(CollectMedia(individual), context, owner, person.Id, eventId: null);

        // Media attached to a source citation on the person (e.g. a scanned certificate).
        await ImportMediaAsync(CollectCitationMedia(individual), context, owner, person.Id, eventId: null);

        foreach (var (source, eventId) in importedEvents)
        {
            var requests = CollectMedia(source).Concat(CollectCitationMedia(source));
            await ImportMediaAsync(requests, context, owner, person.Id, eventId);
        }

        // _PRIM Y wins. Otherwise the first image does, which is what most programs intend.
        var portrait = own.FirstOrDefault(item => item.IsPrimary && item.Kind == GedcomMediaKind.Photo)
            ?? own.FirstOrDefault(item => item.Kind == GedcomMediaKind.Photo);
        if (portrait is not null && string.IsNullOrEmpty(person.ProfileImagePath) && context.Archive is not null)
        {
            await SetProfileImageAsync(person, portrait.EntryName, context);
        }
    }

    private async Task ImportFamilyMediaAsync(
        FamilyRecord family,
        int? husbandId,
        int? wifeId,
        MediaImportContext context)
    {
        // A family photo is tagged on both spouses. Only people created by this import are touched.
        var personIds = new[] { husbandId, wifeId }
            .Where(id => id.HasValue && context.NewPersonIds.Contains(id.Value))
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (personIds.Count == 0)
        {
            return;
        }

        var requests = CollectMedia(family)
            .Concat(CollectCitationMedia(family))
            .Concat(family.Events.SelectMany(familyEvent => CollectMedia(familyEvent).Concat(CollectCitationMedia(familyEvent))));

        await ImportMediaAsync(requests, context, personIds, personIds[0], eventId: null);
    }

    private async Task SetProfileImageAsync(Person person, string entryName, MediaImportContext context)
    {
        try
        {
            await using var stream = context.Archive!.OpenEntry(entryName);
            var (imagePath, _, _) = await fileStorage.SaveProfileImageAsync(stream, person.Id);
            person.ProfileImagePath = imagePath;
            person.UpdatedAtUtc = DateTime.UtcNow;
            await personRepository.UpdateAsync(person);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not set the profile image for GEDCOM person {GedcomId} from {Entry}", person.GedcomId, entryName);
        }
    }

    private async Task<List<ImportedMedia>> ImportMediaAsync(
        IEnumerable<MediaRequest> requests,
        MediaImportContext context,
        IReadOnlyCollection<int> personIds,
        int uploaderId,
        int? eventId)
    {
        var imported = new List<ImportedMedia>();

        foreach (var request in requests)
        {
            var media = await ImportFileAsync(request, context, uploaderId);
            if (media is null)
            {
                continue;
            }

            // Documents and videos belong to the person who uploaded them. Only photos can be tagged on
            // several people, which is how a picture shared by a couple stays one file.
            if (media.Kind == GedcomMediaKind.Photo)
            {
                foreach (int personId in personIds)
                {
                    if (context.PersonLinks.Add((media.Id, personId)))
                    {
                        await personPhotoRepository.InsertAsync(new PersonPhoto { PhotoId = media.Id, PersonId = personId });
                    }
                }

                if (eventId.HasValue && context.EventLinks.Add((media.Id, eventId.Value)))
                {
                    await photoEventRepository.InsertAsync(new PhotoEvent { PhotoId = media.Id, PersonEventId = eventId.Value });
                }
            }

            imported.Add(media);
        }

        return imported;
    }

    /// <summary>Stores one referenced file as a photo, or counts and explains why it could not be. The same file is only stored once.</summary>
    private async Task<ImportedMedia?> ImportFileAsync(MediaRequest request, MediaImportContext context, int uploaderId)
    {
        var lookup = context.Locator.Resolve(request.Reference, request.Format);
        switch (lookup.Status)
        {
            case GedcomMediaLookupStatus.Remote:
                context.Summary.MediaSkipped++;
                context.Warn($"Skipped \"{request.Reference}\": links to web pages and e-mail addresses are not imported.");
                return null;

            case GedcomMediaLookupStatus.NoFileName:
                context.Summary.MediaMissing++;
                context.Warn($"Skipped \"{request.Reference}\": it names a folder, not a file.");
                return null;

            case GedcomMediaLookupStatus.Missing:
                context.Summary.MediaMissing++;
                if (context.Archive is null)
                {
                    context.MissingWithoutArchive++;
                }
                else
                {
                    context.Warn($"\"{request.Reference}\" was not found in the zip file.");
                }

                return null;
        }

        string entryName = lookup.EntryName!;
        if (context.MediaByEntry.TryGetValue(entryName, out var existing))
        {
            return existing;
        }

        if (context.Archive!.GetLength(entryName) > MaxMediaBytes)
        {
            context.Summary.MediaSkipped++;
            context.Warn($"Skipped \"{request.Reference}\": the file is larger than {MaxMediaBytes / (1024 * 1024)} MB.");
            return null;
        }

        string extension = Path.GetExtension(entryName);
        var kind = imageExtensions.Contains(extension)
            ? GedcomMediaKind.Photo
            : videoExtensions.Contains(extension) ? GedcomMediaKind.Video : GedcomMediaKind.Document;

        try
        {
            var media = kind switch
            {
                GedcomMediaKind.Photo => await ImportPhotoAsync(request, context, entryName, uploaderId),
                GedcomMediaKind.Video => await ImportVideoAsync(request, context, entryName, uploaderId),
                _ => await ImportDocumentAsync(request, context, entryName, uploaderId),
            };

            context.MediaByEntry[entryName] = media;
            return media;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not import GEDCOM media {Reference}", request.Reference);
            context.Summary.MediaSkipped++;
            context.Warn($"Skipped \"{request.Reference}\": the file could not be read.");
            return null;
        }
    }

    private async Task<ImportedMedia> ImportPhotoAsync(MediaRequest request, MediaImportContext context, string entryName, int uploaderId)
    {
        await using var stream = context.Archive!.OpenEntry(entryName);
        var (filePath, thumbnailPath, latitude, longitude) = await fileStorage.SaveImageAsync(
            stream,
            Constants.FileStorage.Photos,
            Constants.FileStorage.GedcomImportedMedia);

        bool exactDate = ExactDate(request.Date, out short? takenYear, out byte? takenMonth, out byte? takenDay);
        var photo = await photoRepository.InsertAsync(new Photo
        {
            Title = Truncate(request.Title, 500),
            Description = Caption(request, exactDate),
            YearTaken = exactDate ? takenYear : null,
            MonthTaken = exactDate ? takenMonth : null,
            DayTaken = exactDate ? takenDay : null,
            FilePath = filePath,
            ThumbnailPath = thumbnailPath,
            UploadedByPersonId = uploaderId,
            CreatedAtUtc = DateTime.UtcNow,
            Latitude = latitude,
            Longitude = longitude,
            LatLongAcquiredFromExif = latitude != null && longitude != null,
            GedcomFileReference = Truncate(request.Reference, 1000)
        });

        context.Summary.PhotosImported++;
        return new ImportedMedia(GedcomMediaKind.Photo, photo.Id, entryName, request.IsPrimary);
    }

    private async Task<ImportedMedia> ImportVideoAsync(MediaRequest request, MediaImportContext context, string entryName, int uploaderId)
    {
        string path = await SaveOriginalAsync(context, entryName, Constants.FileStorage.Videos);
        var video = await videoRepository.InsertAsync(new Video
        {
            Title = Truncate(request.Title, 500),
            Description = Caption(request, storeDateSeparately: false),
            FilePath = path,
            UploadedByPersonId = uploaderId,
            CreatedAtUtc = DateTime.UtcNow,
            GedcomFileReference = Truncate(request.Reference, 1000)
        });

        context.Summary.VideosImported++;
        return new ImportedMedia(GedcomMediaKind.Video, video.Id, entryName, request.IsPrimary);
    }

    private async Task<ImportedMedia> ImportDocumentAsync(MediaRequest request, MediaImportContext context, string entryName, int uploaderId)
    {
        long size = context.Archive!.GetLength(entryName);
        string path = await SaveOriginalAsync(context, entryName, Constants.FileStorage.Documents);
        var document = await documentRepository.InsertAsync(new Document
        {
            Title = Truncate(request.Title, 500),
            Description = Caption(request, storeDateSeparately: false),
            FilePath = path,
            ContentType = ContentTypeFor(entryName),
            FileSize = size,
            UploadedByPersonId = uploaderId,
            CreatedAtUtc = DateTime.UtcNow,
            GedcomFileReference = Truncate(request.Reference, 1000)
        });

        context.Summary.DocumentsImported++;
        return new ImportedMedia(GedcomMediaKind.Document, document.Id, entryName, request.IsPrimary);
    }

    private async Task<string> SaveOriginalAsync(MediaImportContext context, string entryName, string category)
    {
        await using var stream = context.Archive!.OpenEntry(entryName);
        return await fileStorage.SaveFileAsync(stream, category, Path.GetFileName(entryName), Constants.FileStorage.GedcomImportedMedia);
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".htm" or ".html" => "text/html",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream",
    };

    private static IEnumerable<MediaRequest> CollectMedia(GedcomCitableView record)
    {
        foreach (var link in record.MediaLinks)
        {
            var media = link.Resolve();
            string? note = FirstNote(media?.Notes ?? link.Notes);
            string? recordDate = media?.Structure.ChildValue(GedcomTags.DATE);
            string? recordText = media?.Structure.ChildValue(GedcomTags.Extensions.TEXT);
            foreach (var file in media?.Files ?? link.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Path))
                {
                    continue;
                }

                bool primary = link.IsPrimary || IsYes(file.Structure.ChildValue(GedcomTags.Extensions.PRIM));
                yield return new MediaRequest(
                    file.Path,
                    file.Format,
                    TitleFor(media, link, file),
                    note,
                    FirstText(file.Structure.ChildValue(GedcomTags.Extensions.DATE), recordDate),
                    FirstText(file.Structure.ChildValue(GedcomTags.Extensions.TEXT), recordText),
                    primary);
            }
        }
    }

    private static IEnumerable<MediaRequest> CollectCitationMedia(GedcomCitableView record) =>
        record.SourceCitations.SelectMany(CollectMedia);

    private static bool IsYes(string? value) => value is not null && value.Trim().StartsWith('Y');

    /// <summary>A title that is only the file name is replaced with the name without its extension.</summary>
    private static string TitleFor(MultimediaRecord? media, MultimediaLink link, MultimediaFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.Title) && !string.Equals(file.Title.Trim(), file.Path, StringComparison.Ordinal))
        {
            return file.Title.Trim();
        }

        string title = (media?.Title ?? link.Title)?.Trim() ?? string.Empty;
        var files = media?.Files ?? link.Files;
        string allFileNames = string.Join(", ", files.Select(item => item.Path));

        if (title.Length == 0 ||
            string.Equals(title, file.Path, StringComparison.Ordinal) ||
            string.Equals(title, allFileNames, StringComparison.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension((file.Path ?? string.Empty).Replace('\\', '/'));
            return string.IsNullOrWhiteSpace(name) ? "Photo" : name;
        }

        return title;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? FirstText(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();

    /// <summary>A calendar date, or a year, with no qualifier such as ABT or FROM.</summary>
    private static bool ExactDate(string? raw, out short? year, out byte? month, out byte? day)
    {
        year = null;
        month = null;
        day = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        foreach (string word in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Equals("ABT", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("ABOUT", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("EST", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("BEF", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("AFT", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("CAL", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("CIRCA", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("INT", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("FROM", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("TO", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("BET", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        (year, month, day) = ParseDateString(raw);
        return year is not null;
    }

    /// <summary>
    /// Photos keep an exact <c>_DATE</c> in the taken-on columns. A phrase, and every date on a video or document,
    /// is written above <c>_TEXT</c> in the description.
    /// </summary>
    private static string? Caption(MediaRequest request, bool storeDateSeparately)
    {
        var parts = new List<string>();
        if (!storeDateSeparately && !string.IsNullOrWhiteSpace(request.Date))
        {
            parts.Add($"Date: {request.Date.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Text))
        {
            parts.Add(request.Text.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Note)
            && !string.Equals(request.Note.Trim(), request.Text?.Trim(), StringComparison.Ordinal))
        {
            parts.Add(request.Note.Trim());
        }

        return parts.Count == 0 ? null : Truncate(string.Join("\n\n", parts), 4000);
    }

    private sealed record MediaRequest(string Reference, string? Format, string Title, string? Note, string? Date, string? Text, bool IsPrimary);

    private enum GedcomMediaKind
    { Photo, Document, Video }

    private sealed record ImportedMedia(GedcomMediaKind Kind, int Id, string EntryName, bool IsPrimary);

    /// <summary>State shared by every record of one import: what was already stored, and what to report.</summary>
    private sealed class MediaImportContext(GedcomMediaArchive? archive, GedcomImportResult summary)
    {
        private static readonly GedcomMediaLocator noFiles = new([], string.Empty);

        private int hiddenWarnings;

        public GedcomMediaArchive? Archive { get; } = archive;

        public GedcomMediaLocator Locator => Archive?.Locator ?? noFiles;

        public GedcomImportResult Summary { get; } = summary;

        /// <summary>People created by this import. Re-imported people keep their existing media.</summary>
        public HashSet<int> NewPersonIds { get; } = [];

        /// <summary>One stored file per zip entry, even when several people or events refer to it.</summary>
        public Dictionary<string, ImportedMedia> MediaByEntry { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<(int PhotoId, int PersonId)> PersonLinks { get; } = [];

        public HashSet<(int PhotoId, int EventId)> EventLinks { get; } = [];

        /// <summary>References to local files when the user uploaded a bare .ged, so no media could be matched.</summary>
        public int MissingWithoutArchive { get; set; }

        public void Warn(string message)
        {
            if (Summary.Warnings.Count < MaxWarnings)
            {
                Summary.Warnings.Add(message);
            }
            else
            {
                hiddenWarnings++;
            }
        }

        public void AddSummaryWarnings()
        {
            if (MissingWithoutArchive > 0)
            {
                Summary.Warnings.Add(
                    $"The GEDCOM refers to {MissingWithoutArchive} media file(s), but only the .ged was uploaded. " +
                    "To import them, upload a .zip or .gdz containing the GEDCOM file and its media folders.");
            }

            if (hiddenWarnings > 0)
            {
                Summary.Warnings.Add($"{hiddenWarnings} more media warning(s) not shown.");
            }
        }
    }
}