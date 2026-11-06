namespace Kinnect.Controllers.Api;

/// <summary>
/// Sets of parents: birth, adopted and foster. Every call is checked for the signed-in user on the server, and a
/// set that user cannot see is answered as if it did not exist.
/// </summary>
[ApiController]
[Authorize]
public class ParentSetApiController(IParentSetService parentSets) : ControllerBase
{
    [HttpGet("api/people/{personId:int}/parent-sets")]
    public async Task<IReadOnlyList<ParentSetDto>> GetForPerson(int personId) => await parentSets.GetForChildAsync(personId);

    [TranslateResultToActionResult]
    [HttpPost("api/people/{personId:int}/parent-sets")]
    public async Task<Result<ParentSetDto>> Add(int personId, [FromBody] ParentSetRequest request) => await parentSets.AddAsync(personId, request);

    [TranslateResultToActionResult]
    [HttpPut("api/parent-sets/{setId:int}")]
    public async Task<Result<ParentSetDto>> Update(int setId, [FromBody] ParentSetRequest request) => await parentSets.UpdateAsync(setId, request);

    [TranslateResultToActionResult]
    [HttpDelete("api/parent-sets/{setId:int}")]
    public async Task<Result> Remove(int setId) => await parentSets.RemoveAsync(setId);
}
