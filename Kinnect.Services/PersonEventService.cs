using Ardalis.Result;
using Kinnect.Models;
using Kinnect.Services.Abstractions;

namespace Kinnect.Services;

public class PersonEventService(IRepository<PersonEvent> eventRepository) : IPersonEventService
{
    public async Task<Result<IEnumerable<PersonEventDto>>> GetByPersonAsync(int personId)
    {
        var events = await eventRepository.FindAsync(new SearchOptions<PersonEvent>
        {
            Query = x => x.PersonId == personId
        });

        return Result.Success(events
            .OrderBy(e => e.Year ?? 9999)
            .ThenBy(e => e.Month ?? 0)
            .ThenBy(e => e.Day ?? 0)
            .Select(MapToDto));
    }

    public async Task<Result<PersonEventDto>> GetByIdAsync(int id)
    {
        var evt = await eventRepository.FindOneAsync(id);
        return evt is null
            ? Result.NotFound("Event not found.")
            : Result.Success(MapToDto(evt));
    }

    public async Task<Result<PersonEventDto>> CreateAsync(int personId, PersonEventRequest request)
    {
        var evt = new PersonEvent
        {
            PersonId = personId,
            EventType = request.EventType.ToUpperInvariant(),
            Year = request.Year,
            Month = request.Month,
            Day = request.Day,
            Place = request.Place,
            Description = request.Description,
            Note = request.Note,
            CreatedAtUtc = DateTime.UtcNow
        };

        await eventRepository.InsertAsync(evt);
        return Result.Success(MapToDto(evt));
    }

    public async Task<Result<PersonEventDto>> UpdateAsync(int id, PersonEventRequest request)
    {
        var evt = await eventRepository.FindOneAsync(id);
        if (evt is null)
            return Result.NotFound("Event not found.");

        evt.EventType = request.EventType.ToUpperInvariant();
        evt.Year = request.Year;
        evt.Month = request.Month;
        evt.Day = request.Day;
        evt.Place = request.Place;
        evt.Description = request.Description;
        evt.Note = request.Note;

        await eventRepository.UpdateAsync(evt);
        return Result.Success(MapToDto(evt));
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var evt = await eventRepository.FindOneAsync(id);
        if (evt is null)
            return Result.NotFound("Event not found.");

        await eventRepository.DeleteAsync(evt);
        return Result.Success();
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
        Description = e.Description,
        Note = e.Note,
        CreatedAtUtc = e.CreatedAtUtc
    };
}
