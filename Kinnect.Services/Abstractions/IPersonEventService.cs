namespace Kinnect.Services.Abstractions;

public interface IPersonEventService
{
    Task<Result<PersonEventDto>> CopyToPersonAsync(int sourceEventId, int targetPersonId);

    Task<Result<PersonEventDto>> CreateAsync(int personId, PersonEventRequest request);

    Task<Result> DeleteAsync(int id);

    Task<Result<PersonEventDto>> GetByIdAsync(int id);

    Task<Result<IEnumerable<PersonEventDto>>> GetByPersonAsync(int personId);

    Task<Result<PersonEventDto>> UpdateAsync(int id, PersonEventRequest request);
}