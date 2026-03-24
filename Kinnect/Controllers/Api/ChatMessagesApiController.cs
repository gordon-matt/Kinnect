using Kinnect.Data;
using Kinnect.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/chat-messages")]
[Authorize]
public class ChatMessagesApiController(
    ApplicationDbContext dbContext,
    IHubContext<ChatHub> hubContext) : ControllerBase
{
    private string CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException("User is not authenticated.");

    [HttpGet("room/{roomId:int}")]
    public async Task<IActionResult> GetRoomMessages(int roomId, [FromQuery] int take = 50)
    {
        var rawMessages = await dbContext.ChatMessages
            .Where(m => m.ToRoomId == roomId)
            .Include(m => m.FromUser)
            .OrderByDescending(m => m.Timestamp)
            .Take(take)
            .OrderBy(m => m.Timestamp)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.Timestamp,
                m.FromUserId,
                fromUserName = m.FromUser.UserName,
                m.ToRoomId
            })
            .ToListAsync();

        var userIds = rawMessages.Select(m => m.FromUserId).Distinct().ToList();
        var fullNamesByUserId = await dbContext.People
            .Where(p => p.UserId != null && userIds.Contains(p.UserId))
            .Select(p => new { p.UserId, FullName = (p.GivenNames + " " + p.FamilyName).Trim() })
            .ToDictionaryAsync(x => x.UserId!, x => x.FullName);

        var messages = rawMessages.Select(m => new
        {
            m.Id,
            m.Content,
            m.Timestamp,
            m.FromUserId,
            m.fromUserName,
            fromFullName = fullNamesByUserId.GetValueOrDefault(m.FromUserId) ?? m.fromUserName,
            m.ToRoomId
        });

        return Ok(messages);
    }

    [HttpGet("private/{otherUserId}")]
    public async Task<IActionResult> GetPrivateMessages(string otherUserId, [FromQuery] int take = 50)
    {
        string me = CurrentUserId;
        var rawMessages = await dbContext.ChatMessages
            .Where(m => m.ToUserId != null &&
                ((m.FromUserId == me && m.ToUserId == otherUserId) ||
                 (m.FromUserId == otherUserId && m.ToUserId == me)))
            .Include(m => m.FromUser)
            .OrderByDescending(m => m.Timestamp)
            .Take(take)
            .OrderBy(m => m.Timestamp)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.Timestamp,
                m.FromUserId,
                fromUserName = m.FromUser.UserName,
                m.ToUserId
            })
            .ToListAsync();

        var userIds = rawMessages.Select(m => m.FromUserId).Distinct().ToList();
        var fullNamesByUserId = await dbContext.People
            .Where(p => p.UserId != null && userIds.Contains(p.UserId))
            .Select(p => new { p.UserId, FullName = (p.GivenNames + " " + p.FamilyName).Trim() })
            .ToDictionaryAsync(x => x.UserId!, x => x.FullName);

        var messages = rawMessages.Select(m => new
        {
            m.Id,
            m.Content,
            m.Timestamp,
            m.FromUserId,
            m.fromUserName,
            fromFullName = fullNamesByUserId.GetValueOrDefault(m.FromUserId) ?? m.fromUserName,
            m.ToUserId
        });

        return Ok(messages);
    }

    [HttpPost("room")]
    public async Task<IActionResult> PostToRoom([FromBody] RoomMessageRequest request)
    {
        var room = await dbContext.ChatRooms.FindAsync(request.RoomId);
        if (room is null) return BadRequest("Room not found.");

        var msg = new ChatMessage
        {
            Content = System.Text.RegularExpressions.Regex.Replace(request.Content, @"<.*?>", string.Empty),
            Timestamp = DateTime.UtcNow,
            FromUserId = CurrentUserId,
            ToRoomId = room.Id
        };
        dbContext.ChatMessages.Add(msg);
        await dbContext.SaveChangesAsync();

        var fromPerson = dbContext.People
            .Where(p => p.UserId == CurrentUserId)
            .Select(p => new { p.GivenNames, p.FamilyName })
            .FirstOrDefault();
        var fromUser = await dbContext.Users.FindAsync(CurrentUserId);
        string fromFullName = fromPerson is not null
            ? $"{fromPerson.GivenNames} {fromPerson.FamilyName}".Trim()
            : fromUser?.UserName ?? CurrentUserId;

        var vm = new
        {
            msg.Id,
            msg.Content,
            msg.Timestamp,
            msg.FromUserId,
            fromUserName = fromUser?.UserName,
            fromFullName,
            msg.ToRoomId
        };

        await hubContext.Clients.Group(room.Name).SendAsync("newMessage", vm);

        return Ok(vm);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var msg = await dbContext.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == id && m.FromUserId == CurrentUserId);
        if (msg is null) return NotFound();

        int? roomId = msg.ToRoomId;
        string? roomName = null;
        if (roomId.HasValue)
        {
            roomName = (await dbContext.ChatRooms.FindAsync(roomId.Value))?.Name;
        }

        dbContext.ChatMessages.Remove(msg);
        await dbContext.SaveChangesAsync();

        if (roomName is not null)
            await hubContext.Clients.Group(roomName).SendAsync("removeChatMessage", id);

        return Ok();
    }

    public sealed record RoomMessageRequest(int RoomId, string Content);
}
