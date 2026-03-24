namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/people/{personId:int}/events")]
[Authorize]
public class PersonEventApiController(IPersonEventService personEventService) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet]
    public async Task<Result<IEnumerable<PersonEventDto>>> GetByPerson(int personId) =>
        await personEventService.GetByPersonAsync(personId);

    [TranslateResultToActionResult]
    [HttpPost]
    public async Task<Result<PersonEventDto>> Create(int personId, [FromBody] PersonEventRequest request) =>
        await personEventService.CreateAsync(personId, request);

    [TranslateResultToActionResult]
    [HttpPut("{eventId:int}")]
    public async Task<Result<PersonEventDto>> Update(int personId, int eventId, [FromBody] PersonEventRequest request) =>
        await personEventService.UpdateAsync(eventId, request);

    [TranslateResultToActionResult]
    [HttpDelete("{eventId:int}")]
    public async Task<Result> Delete(int personId, int eventId) =>
        await personEventService.DeleteAsync(eventId);

    [TranslateResultToActionResult]
    [HttpPost("{eventId:int}/copy/{targetPersonId:int}")]
    public async Task<Result<PersonEventDto>> CopyToTarget(int personId, int eventId, int targetPersonId) =>
        await personEventService.CopyToPersonAsync(eventId, targetPersonId);
}