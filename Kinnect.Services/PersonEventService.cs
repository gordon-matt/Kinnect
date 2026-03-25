namespace Kinnect.Services;

public class PersonEventService(IRepository<PersonEvent> eventRepository) : IPersonEventService
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

        var copy = new PersonEvent
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
        };

        await eventRepository.InsertAsync(copy);
        return Result.Success(MapToDto(copy));
    }

    public async Task<Result<PersonEventDto>> CreateAsync(int personId, PersonEventRequest request)
    {
        var eventType = request.EventType.ToUpperInvariant();
        if (PersonEventType.IsNonTimelineEventType(eventType))
        {
            return Result.Invalid(new ValidationError("This event type is not stored on the timeline."));
        }

        if (await HasSingleInstanceConflictAsync(personId, eventType))
        {
            return Result.Invalid(new ValidationError(
                $"{PersonEventType.GetLabel(eventType)} can only be added once."));
        }

        var evt = new PersonEvent
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
        };

        await eventRepository.InsertAsync(evt);
        return Result.Success(MapToDto(evt));
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
            : Result.Success(MapToDto(evt));
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
            .Select(MapToDto));
    }

    public async Task<Result<PersonEventDto>> UpdateAsync(int id, PersonEventRequest request)
    {
        var evt = await eventRepository.FindOneAsync(id);
        if (evt is null)
        {
            return Result.NotFound("Event not found.");
        }

        var eventType = request.EventType.ToUpperInvariant();
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
        return Result.Success(MapToDto(evt));
    }

    private static PersonEventDto MapToDto(PersonEvent e) => new()
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

    private async Task<bool> HasSingleInstanceConflictAsync(int personId, string eventType, int? excludingEventId = null)
    {
        if (!PersonEventType.IsSingleInstanceTimelineEventType(eventType))
        {
            return false;
        }

        var existing = await eventRepository.FindAsync(new SearchOptions<PersonEvent>
        {
            Query = e => e.PersonId == personId
                && e.EventType == eventType
                && (!excludingEventId.HasValue || e.Id != excludingEventId.Value),
        });

        return existing.Any();
    }
}