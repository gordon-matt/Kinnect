using Kinnect.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/chat-messages")]
[Authorize]
public class ChatMessagesApiController(
    IChatService chatService,
    IUserContextService userContextService,
    IHubContext<ChatHub> hubContext) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet("room/{roomId:int}")]
    public async Task<Result<IEnumerable<ChatMessageDto>>> GetRoomMessages(int roomId, [FromQuery] int take = 50) =>
        await chatService.GetRoomMessagesAsync(roomId, take);

    [TranslateResultToActionResult]
    [HttpGet("private/{otherUserId}")]
    public async Task<Result<IEnumerable<ChatMessageDto>>> GetPrivateMessages(string otherUserId, [FromQuery] int take = 50)
    {
        string? currentUserId = userContextService.GetCurrentUserId();
        return currentUserId is null
            ? (Result<IEnumerable<ChatMessageDto>>)Result.Unauthorized()
            : await chatService.GetPrivateMessagesAsync(currentUserId, otherUserId, take);
    }

    [TranslateResultToActionResult]
    [HttpPost("room")]
    public async Task<Result<ChatMessageDto>> PostToRoom([FromBody] ChatRoomMessageCreateRequest request)
    {
        string? currentUserId = userContextService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Result.Unauthorized();
        }

        var result = await chatService.CreateRoomMessageAsync(request.RoomId, request.Content, currentUserId);
        if (result.IsSuccess && result.Value is not null && result.Value.ToRoomName is not null)
        {
            await hubContext.Clients.Group(result.Value.ToRoomName).SendAsync("newMessage", result.Value);
        }

        return result;
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    public async Task<Result<ChatDeleteMessageDto>> Delete(int id)
    {
        string? currentUserId = userContextService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Result.Unauthorized();
        }

        var result = await chatService.DeleteMessageAsync(id, currentUserId);
        if (result.IsSuccess && result.Value?.RoomName is not null)
        {
            await hubContext.Clients.Group(result.Value.RoomName).SendAsync("removeChatMessage", id);
        }

        return result;
    }
}
