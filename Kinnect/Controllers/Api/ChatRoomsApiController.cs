using Kinnect.Data;
using Kinnect.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/chat-rooms")]
[Authorize]
public class ChatRoomsApiController(
    ApplicationDbContext dbContext,
    IHubContext<ChatHub> hubContext) : ControllerBase
{
    private string CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException("User is not authenticated.");

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rooms = await dbContext.ChatRooms
            .Include(r => r.Admin)
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, adminUserId = r.AdminUserId, adminUserName = r.Admin.UserName })
            .ToListAsync();

        return Ok(rooms);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var room = await dbContext.ChatRooms.Include(r => r.Admin).FirstOrDefaultAsync(r => r.Id == id);
        if (room is null) return NotFound();
        return Ok(new { room.Id, room.Name, adminUserId = room.AdminUserId });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ChatRoomRequest request)
    {
        if (await dbContext.ChatRooms.AnyAsync(r => r.Name == request.Name))
            return BadRequest("A room with that name already exists.");

        var room = new ChatRoom
        {
            Name = request.Name,
            AdminUserId = CurrentUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.ChatRooms.Add(room);
        await dbContext.SaveChangesAsync();

        var vm = new { room.Id, room.Name, adminUserId = room.AdminUserId };
        await hubContext.Clients.All.SendAsync("addChatRoom", vm);

        return CreatedAtAction(nameof(Get), new { id = room.Id }, vm);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, [FromBody] ChatRoomRequest request)
    {
        var room = await dbContext.ChatRooms.FirstOrDefaultAsync(r => r.Id == id && r.AdminUserId == CurrentUserId);
        if (room is null) return NotFound();

        if (await dbContext.ChatRooms.AnyAsync(r => r.Name == request.Name && r.Id != id))
            return BadRequest("A room with that name already exists.");

        room.Name = request.Name;
        await dbContext.SaveChangesAsync();

        var vm = new { room.Id, room.Name, adminUserId = room.AdminUserId };
        await hubContext.Clients.All.SendAsync("updateChatRoom", vm);

        return Ok(vm);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await dbContext.ChatRooms.FirstOrDefaultAsync(r => r.Id == id && r.AdminUserId == CurrentUserId);
        if (room is null) return NotFound();

        dbContext.ChatRooms.Remove(room);
        await dbContext.SaveChangesAsync();

        await hubContext.Clients.All.SendAsync("removeChatRoom", id);
        await hubContext.Clients.Group(room.Name).SendAsync("onRoomDeleted");

        return Ok();
    }

    public sealed record ChatRoomRequest(string Name);
}
