namespace Kinnect.Services;

public class PersonEventService(
    IRepository<PersonEvent> eventRepository,
    IRepository<Person> personRepository,
    IRepository<PersonSpouse> spouseRepository) : IPersonEventService
{
    public async Task<Result<PersonEventDto>> CopyToPersonAsync(int sourceEventId, int targetPersonId)
    {
        var source = await eventRepository.FindOneAsync(sourceEventId);
        if (source is null)
        {
            return Result.NotFound("Event not found.");
        }

        if (await HasSingleInstanceConflictAsync(targetPersonId, source.EventType))
        {
            return Result.Invalid(new ValidationError(
                $"{PersonEventType.GetLabel(source.EventType)} can only be added once."));
        }

        var copy = await eventRepository.InsertAsync(new PersonEvent
        {
            PersonId = targetPersonId,
            EventType = source.EventType,
            Year = source.Year,
            Month = source.Month,
            Day = source.Day,
            Place = source.Place,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            Description = source.Description,
            Note = source.Note,
            CreatedAtUtc = DateTime.UtcNow
        });

        return Result.Success(copy.ToDto());
    }

    public async Task<Result<int>> CopyToCloseRelativesAsync(
        int sourceEventId,
        IReadOnlyCollection<int> targetPersonIds,
        string currentUserId,
        bool isAdmin = false,
        bool isEditorOrAbove = false)
    {
        if (targetPersonIds.Count == 0)
        {
            return Result.Invalid(new ValidationError("Choose at least one relative."));
        }

        var source = await eventRepository.FindOneAsync(sourceEventId);
        if (source is null)
        {
            return Result.NotFound("Event not found.");
        }

        if (!PersonEventType.CanCopyToRelatives(source.EventType))
        {
            return Result.Invalid(new ValidationError(
                $"{PersonEventType.GetLabel(source.EventType)} belongs to one person and cannot be copied."));
        }

        var relatives = await GetCloseRelativesAsync(source.PersonId);
        if (!relatives.IsSuccess)
        {
            return Result<int>.NotFound(relatives.Errors.FirstOrDefault() ?? "Person not found.");
        }

        var allowed = relatives.Value.Select(relative => relative.PersonId).ToHashSet();
        int copied = 0;
        int denied = 0;
        int duplicates = 0;

        foreach (int targetId in targetPersonIds.Distinct())
        {
            if (!allowed.Contains(targetId))
            {
                continue;
            }

            var target = await personRepository.FindOneAsync(targetId);
            if (target is null)
            {
                continue;
            }

            bool canEdit = target.UserId == currentUserId
                || isAdmin
                || (isEditorOrAbove && target.UserId is null);
            if (!canEdit)
            {
                denied++;
                continue;
            }

            if (await HasSameEventAsync(targetId, source))
            {
                duplicates++;
                continue;
            }

            await eventRepository.InsertAsync(new PersonEvent
            {
                PersonId = targetId,
                EventType = source.EventType,
                Year = source.Year,
                Month = source.Month,
                Day = source.Day,
                Place = source.Place,
                Latitude = source.Latitude,
                Longitude = source.Longitude,
                Description = source.Description,
                Note = source.Note,
                CreatedAtUtc = DateTime.UtcNow
            });
            copied++;
        }

        if (copied > 0)
        {
            return Result.Success(copied);
        }

        if (denied > 0)
        {
            return Result.Forbidden();
        }

        if (duplicates > 0)
        {
            return Result.Success(0);
        }

        return Result.Invalid(new ValidationError("Choose a spouse, parent, sibling, or child."));
    }

    public async Task<Result<IReadOnlyList<CloseRelativeDto>>> GetCloseRelativesAsync(int personId)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        var spouseLinks = await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>
        {
            Query = x => x.PersonId == personId || x.SpouseId == personId
        });

        var spouseIds = spouseLinks
            .Select(link => link.PersonId == personId ? link.SpouseId : link.PersonId)
            .ToHashSet();

        int? fatherId = person.FatherId;
        int? motherId = person.MotherId;

        var candidates = (await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = other => other.Id != personId && (
                spouseIds.Contains(other.Id)
                || (fatherId != null && other.Id == fatherId)
                || (motherId != null && other.Id == motherId)
                || other.FatherId == personId
                || other.MotherId == personId
                || (fatherId != null && other.FatherId == fatherId)
                || (motherId != null && other.MotherId == motherId))
        })).ToList();

        string[] order = ["Spouse", "Father", "Mother", "Child", "Sibling"];
        IReadOnlyList<CloseRelativeDto> relatives = candidates
            .Select(other => (Person: other, Relationship: RelationshipOf(other)))
            .Where(row => row.Relationship is not null)
            .OrderBy(row => Array.IndexOf(order, row.Relationship))
            .ThenBy(row => row.Person.GivenNames, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Person.FamilyName, StringComparer.OrdinalIgnoreCase)
            .Select(row => new CloseRelativeDto
            {
                PersonId = row.Person.Id,
                FullName = $"{row.Person.GivenNames} {row.Person.FamilyName}",
                Relationship = row.Relationship!
            })
            .ToList();

        return Result.Success(relatives);

        string? RelationshipOf(Person other)
        {
            if (spouseIds.Contains(other.Id))
            {
                return "Spouse";
            }

            if (other.Id == fatherId)
            {
                return "Father";
            }

            if (other.Id == motherId)
            {
                return "Mother";
            }

            if (other.FatherId == personId || other.MotherId == personId)
            {
                return "Child";
            }

            if ((fatherId != null && other.FatherId == fatherId) || (motherId != null && other.MotherId == motherId))
            {
                return "Sibling";
            }

            return null;
        }
    }

    public async Task<Result<PersonEventDto>> CreateAsync(int personId, PersonEventRequest request)
    {
        string eventType = request.EventType.ToUpperInvariant();
        if (PersonEventType.IsNonTimelineEventType(eventType))
        {
            return Result.Invalid(new ValidationError("This event type is not stored on the timeline."));
        }

        if (await HasSingleInstanceConflictAsync(personId, eventType))
        {
            return Result.Invalid(new ValidationError(
                $"{PersonEventType.GetLabel(eventType)} can only be added once."));
        }

        var evt = await eventRepository.InsertAsync(new PersonEvent
        {
            PersonId = personId,
            EventType = eventType,
            Year = request.Year,
            Month = request.Month,
            Day = request.Day,
            Place = request.Place,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Description = request.Description,
            Note = request.Note,
            CreatedAtUtc = DateTime.UtcNow
        });

        return Result.Success(evt.ToDto());
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var evt = await eventRepository.FindOneAsync(id);
        if (evt is null)
        {
            return Result.NotFound("Event not found.");
        }

        await eventRepository.DeleteAsync(evt);
        return Result.Success();
    }

    public async Task<Result<PersonEventDto>> GetByIdAsync(int id)
    {
        var evt = await eventRepository.FindOneAsync(id);
        return evt is null
            ? Result.NotFound("Event not found.")
            : Result.Success(evt.ToDto());
    }

    public async Task<Result<IEnumerable<PersonEventDto>>> GetByPersonAsync(int personId)
    {
        var events = await eventRepository.FindAsync(new SearchOptions<PersonEvent>
        {
            Query = x => x.PersonId == personId
        });

        return Result.Success(events
            .Where(e => !PersonEventType.IsNonTimelineEventType(e.EventType))
            .OrderBy(e => e.Year ?? 9999)
            .ThenBy(e => e.Month ?? 0)
            .ThenBy(e => e.Day ?? 0)
            .Select(e => e.ToDto()));
    }

    public async Task<Result<PersonEventDto>> UpdateAsync(int id, PersonEventRequest request)
    {
        var evt = await eventRepository.FindOneAsync(id);
        if (evt is null)
        {
            return Result.NotFound("Event not found.");
        }

        string eventType = request.EventType.ToUpperInvariant();

        if (PersonEventType.IsNonTimelineEventType(eventType))
        {
            return Result.Invalid(new ValidationError("This event type is not stored on the timeline."));
        }

        if (await HasSingleInstanceConflictAsync(evt.PersonId, eventType, evt.Id))
        {
            return Result.Invalid(new ValidationError(
                $"{PersonEventType.GetLabel(eventType)} can only be added once."));
        }

        evt.EventType = eventType;
        evt.Year = request.Year;
        evt.Month = request.Month;
        evt.Day = request.Day;
        evt.Place = request.Place;
        evt.Latitude = request.Latitude;
        evt.Longitude = request.Longitude;
        evt.Description = request.Description;
        evt.Note = request.Note;

        await eventRepository.UpdateAsync(evt);
        return Result.Success(evt.ToDto());
    }

    private async Task<bool> HasSingleInstanceConflictAsync(int personId, string eventType, int? excludingEventId = null)
    {
        if (!PersonEventType.IsSingleInstanceTimelineEventType(eventType))
        {
            return false;
        }

        return await eventRepository.ExistsAsync(e =>
            e.PersonId == personId &&
            e.EventType == eventType &&
            (
                !excludingEventId.HasValue ||
                e.Id != excludingEventId.Value
            ));
    }

    private async Task<bool> HasSameEventAsync(int personId, PersonEvent source)
    {
        var existing = await eventRepository.FindAsync(new SearchOptions<PersonEvent>
        {
            Query = evt => evt.PersonId == personId && evt.EventType == source.EventType
        });

        return existing.Any(evt =>
            evt.Year == source.Year
            && evt.Month == source.Month
            && evt.Day == source.Day
            && SameText(evt.Place, source.Place)
            && SameText(evt.Description, source.Description));
    }

    private static bool SameText(string? left, string? right) =>
        string.Equals((left ?? string.Empty).Trim(), (right ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
}