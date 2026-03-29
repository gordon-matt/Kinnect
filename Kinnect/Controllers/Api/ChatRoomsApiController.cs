using Kinnect.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/chat-rooms")]
[Authorize]
public class ChatRoomsApiController(
    IChatService chatService,
    IUserContextService userContextService,
    IHubContext<ChatHub> hubContext) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet("{id:int}")]
    public async Task<Result<ChatRoomDto>> Get(int id) =>
        await chatService.GetRoomByIdAsync(id);

    [TranslateResultToActionResult]
    [HttpGet]
    public async Task<Result<IEnumerable<ChatRoomDto>>> GetAll() =>
        await chatService.GetRoomsAsync();

    [TranslateResultToActionResult]
    [HttpPost]
    [Authorize(Roles = Constants.Roles.AdministratorOrEditor)]
    public async Task<Result<ChatRoomDto>> Create([FromBody] ChatRoomUpsertRequest request)
    {
        string? currentUserId = userContextService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Result.Unauthorized();
        }

        var result = await chatService.CreateRoomAsync(request.Name, currentUserId);
        if (result.IsSuccess && result.Value is not null)
        {
            await hubContext.Clients.All.SendAsync("addChatRoom", result.Value);
        }

        return result;
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    public async Task<Result<ChatDeleteRoomDto>> Delete(int id)
    {
        string? currentUserId = userContextService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Result.Unauthorized();
        }

        var result = await chatService.DeleteRoomAsync(id, currentUserId, userContextService.IsAdmin());
        if (result.IsSuccess && result.Value is not null)
        {
            await hubContext.Clients.All.SendAsync("removeChatRoom", result.Value.RoomId);
            await hubContext.Clients.Group(result.Value.RoomName).SendAsync("onRoomDeleted");
        }

        return result;
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}")]
    public async Task<Result<ChatRoomDto>> Edit(int id, [FromBody] ChatRoomUpsertRequest request)
    {
        string? currentUserId = userContextService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Result.Unauthorized();
        }

        var result = await chatService.UpdateRoomAsync(id, request.Name, currentUserId);
        if (result.IsSuccess && result.Value is not null)
        {
            await hubContext.Clients.All.SendAsync("updateChatRoom", result.Value);
        }

        return result;
    }
}