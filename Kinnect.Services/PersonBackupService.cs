using System.Text.Json;
using Kinnect.Data;
using Kinnect.Models.Backup;
using Kinnect.Models.Dto.Admin;

namespace Kinnect.Services;

public sealed class PersonBackupService(
    ApplicationDbContext db,
    IFileStorageService fileStorageService,
    ILogger<PersonBackupService> logger) : IPersonBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task ExportToNewFileAsync(CancellationToken cancellationToken = default)
    {
        string dir = ResolveBackupDirectory();
        Directory.CreateDirectory(dir);

        var people = await db.People.AsNoTracking().OrderBy(p => p.Id).ToListAsync(cancellationToken);
        var spouses = await db.PersonSpouses.AsNoTracking().ToListAsync(cancellationToken);

        var doc = new PersonTreeBackupDocument
        {
            ExportedAtUtc = DateTime.UtcNow,
            People = people.Select(p => new PersonBackupEntry
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
                IsDeceased = p.IsDeceased,
                GedcomId = p.GedcomId,
                CreatedAtUtc = p.CreatedAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc
            }).ToList(),
            Spouses = spouses.Select(s => new PersonSpouseBackupEntry
            {
                PersonId = s.PersonId,
                SpouseId = s.SpouseId,
                MarriageYear = s.MarriageYear,
                MarriageMonth = s.MarriageMonth,
                MarriageDay = s.MarriageDay,
                DivorceYear = s.DivorceYear,
                DivorceMonth = s.DivorceMonth,
                DivorceDay = s.DivorceDay,
                EngagementYear = s.EngagementYear,
                EngagementMonth = s.EngagementMonth,
                EngagementDay = s.EngagementDay,
                HasEngagement = s.HasEngagement,
                HasMarriage = s.HasMarriage,
                HasDivorce = s.HasDivorce
            }).ToList()
        };

        string name = $"person-backup-{doc.ExportedAtUtc:yyyyMMdd-HHmmss}.json";
        string path = Path.Combine(dir, name);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(doc, JsonOptions), cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Person tree backup written to {Path} ({Count} people, {SpouseCount} spouse links).",
                path, doc.People.Count, doc.Spouses.Count);
        }
    }

    public async Task<Result<IReadOnlyList<PersonBackupFileDto>>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        string dir = ResolveBackupDirectory();
        if (!Directory.Exists(dir))
        {
            return Result.Success<IReadOnlyList<PersonBackupFileDto>>([]);
        }

        var infos = new DirectoryInfo(dir).GetFiles("person-backup-*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new PersonBackupFileDto
            {
                FileName = f.Name,
                SizeBytes = f.Length,
                CreatedUtc = f.LastWriteTimeUtc
            })
            .ToList();

        return Result.Success<IReadOnlyList<PersonBackupFileDto>>(infos);
    }

    public async Task<Result> RestoreFromFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (!TryResolveExistingBackupFile(fileName, out string? path, out string? error))
        {
            return Result.Invalid(new ValidationError(error ?? "Invalid backup file."));
        }

        PersonTreeBackupDocument? doc;
        try
        {
            await using var stream = File.OpenRead(path!);
            doc = await JsonSerializer.DeserializeAsync<PersonTreeBackupDocument>(stream, JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read person backup {File}", fileName);
            return Result.Error("Could not read or parse the backup file.");
        }

        if (doc is null || doc.SchemaVersion != 1)
        {
            return Result.Invalid(new ValidationError("Unsupported or empty backup file."));
        }

        if (doc.People.Count == 0)
        {
            return Result.Invalid(new ValidationError("Backup contains no people."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingIds = await db.People.Select(p => p.Id).ToHashSetAsync(cancellationToken);

            await db.PersonSpouses.ExecuteDeleteAsync(cancellationToken);

            foreach (var entry in doc.People.OrderBy(p => p.Id))
            {
                var entity = await db.People.FirstOrDefaultAsync(p => p.Id == entry.Id, cancellationToken);
                if (entity is null)
                {
                    continue;
                }

                entity.UserId = entry.UserId;
                entity.FamilyName = entry.FamilyName;
                entity.GivenNames = entry.GivenNames;
                entity.IsMale = entry.IsMale;
                entity.Bio = entry.Bio;
                entity.ProfileImagePath = entry.ProfileImagePath;
                entity.Latitude = entry.Latitude;
                entity.Longitude = entry.Longitude;
                entity.FatherId = ValidParent(entry.FatherId, existingIds);
                entity.MotherId = ValidParent(entry.MotherId, existingIds);
                entity.Occupation = entry.Occupation;
                entity.Education = entry.Education;
                entity.Religion = entry.Religion;
                entity.Note = entry.Note;
                entity.IsDeceased = entry.IsDeceased;
                entity.GedcomId = entry.GedcomId;
                entity.CreatedAtUtc = entry.CreatedAtUtc;
                entity.UpdatedAtUtc = entry.UpdatedAtUtc;
            }

            await db.SaveChangesAsync(cancellationToken);

            foreach (var s in doc.Spouses)
            {
                if (!existingIds.Contains(s.PersonId) || !existingIds.Contains(s.SpouseId))
                {
                    continue;
                }

                db.PersonSpouses.Add(new PersonSpouse
                {
                    PersonId = s.PersonId,
                    SpouseId = s.SpouseId,
                    MarriageYear = s.MarriageYear,
                    MarriageMonth = s.MarriageMonth,
                    MarriageDay = s.MarriageDay,
                    DivorceYear = s.DivorceYear,
                    DivorceMonth = s.DivorceMonth,
                    DivorceDay = s.DivorceDay,
                    EngagementYear = s.EngagementYear,
                    EngagementMonth = s.EngagementMonth,
                    EngagementDay = s.EngagementDay,
                    HasEngagement = s.HasEngagement,
                    HasMarriage = s.HasMarriage,
                    HasDivorce = s.HasDivorce
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            int peopleTouched = doc.People.Count(e => existingIds.Contains(e.Id));
            int spouseRows = doc.Spouses.Count(s => existingIds.Contains(s.PersonId) && existingIds.Contains(s.SpouseId));
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Person tree restored from {File}. {PeopleTouched} people updated; {SpouseRows} spouse rows applied.",
                    fileName, peopleTouched, spouseRows);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Person tree restore failed for {File}", fileName);
            return Result.Error("Restore failed. No changes were applied.");
        }
    }

    private string ResolveBackupDirectory() =>
        Path.GetFullPath(Path.Combine(fileStorageService.GetBaseUploadPath(), Constants.FileStorage.PersonTreeBackups));

    private bool TryResolveExistingBackupFile(string fileName, out string? fullPath, out string? error)
    {
        fullPath = null;
        error = null;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "File name is required.";
            return false;
        }

        string safe = Path.GetFileName(fileName);
        if (!safe.Equals(fileName, StringComparison.Ordinal) || !safe.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            error = "Invalid file name.";
            return false;
        }

        if (!safe.StartsWith("person-backup-", StringComparison.OrdinalIgnoreCase))
        {
            error = "Not a recognized backup file.";
            return false;
        }

        string dir = ResolveBackupDirectory();
        string root = Path.GetFullPath(dir);
        string candidate = Path.GetFullPath(Path.Combine(dir, safe));
        string relative = Path.GetRelativePath(root, candidate);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            error = "Invalid path.";
            return false;
        }

        if (!File.Exists(candidate))
        {
            error = "Backup file not found.";
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private static int? ValidParent(int? id, HashSet<int> existingIds) =>
        id.HasValue && existingIds.Contains(id.Value) ? id : null;
}