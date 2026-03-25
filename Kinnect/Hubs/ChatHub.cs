using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Kinnect.Hubs;

[Authorize]
public class ChatHub(IChatService chatService, IUserContextService userContextService) : Hub
{
    // In-memory presence tracking keyed by ConnectionId
    private static readonly Dictionary<string, ConnectedUser> Connections = new();

    private static readonly Lock ConnectionsLock = new();

    private string CurrentUserId =>
        Context.UserIdentifier
        ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? Context.User?.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("User is not authenticated.");

    public IEnumerable<object> GetUsers(string roomName)
    {
        lock (ConnectionsLock)
        {
            return Connections.Values
                .Where(c => c.CurrentRoom == roomName)
                .Select(BuildUserVm)
                .ToList();
        }
    }

    public async Task Join(string roomName)
    {
        ConnectedUser? conn;
        lock (ConnectionsLock)
        {
            Connections.TryGetValue(Context.ConnectionId, out conn);
        }

        if (conn is null) return;

        if (!string.IsNullOrEmpty(conn.CurrentRoom))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conn.CurrentRoom);
            await Clients.OthersInGroup(conn.CurrentRoom).SendAsync("removeUser", BuildUserVm(conn));
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        lock (ConnectionsLock)
        {
            conn.CurrentRoom = roomName;
        }

        await Clients.OthersInGroup(roomName).SendAsync("addUser", BuildUserVm(conn));
    }

    public async Task Leave(string roomName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
    }

    public override async Task OnConnectedAsync()
    {
        string userId = CurrentUserId;
        string userName = Context.User?.Identity?.Name ?? userId;
        bool isAdmin = userContextService.IsAdmin();
        var profileResult = await chatService.GetCurrentChatUserAsync(userId, userName);

        var conn = new ConnectedUser
        {
            UserId = userId,
            UserName = userName,
            FullName = profileResult.IsSuccess && profileResult.Value is not null
                ? profileResult.Value.FullName
                : userName,
            IsAdmin = isAdmin,
            ConnectionId = Context.ConnectionId,
            CurrentRoom = string.Empty
        };

        lock (ConnectionsLock)
        {
            Connections[Context.ConnectionId] = conn;
        }

        await Clients.Caller.SendAsync("getProfileInfo", BuildUserVm(conn));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectedUser? conn;
        lock (ConnectionsLock)
        {
            Connections.TryGetValue(Context.ConnectionId, out conn);
            Connections.Remove(Context.ConnectionId);
        }

        if (conn is not null && !string.IsNullOrEmpty(conn.CurrentRoom))
            await Clients.OthersInGroup(conn.CurrentRoom).SendAsync("removeUser", BuildUserVm(conn));

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendPrivate(string toUserId, string message)
    {
        var result = await chatService.CreatePrivateMessageAsync(CurrentUserId, toUserId, message);
        if (!result.IsSuccess || result.Value is null) return;
        var vm = result.Value;

        // Deliver to recipient's active connection and back to sender
        string? recipientConnectionId = GetConnectionId(toUserId);
        if (recipientConnectionId is not null)
            await Clients.Client(recipientConnectionId).SendAsync("newPrivateMessage", vm);

        await Clients.Caller.SendAsync("newPrivateMessage", vm);
    }

    private static object BuildUserVm(ConnectedUser c) => new
    {
        userId = c.UserId,
        userName = c.UserName,
        fullName = c.FullName,
        isAdmin = c.IsAdmin,
        currentRoom = c.CurrentRoom
    };

    private static string? GetConnectionId(string userId)
    {
        lock (ConnectionsLock)
        {
            return Connections.FirstOrDefault(kv => kv.Value.UserId == userId).Key;
        }
    }

    private sealed class ConnectedUser
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string? CurrentRoom { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}