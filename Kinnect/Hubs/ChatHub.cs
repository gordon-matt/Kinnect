using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Kinnect.Hubs;

[Authorize]
public class ChatHub(
    IChatService chatService,
    IUserContextService userContextService,
    IHubContext<NotificationHub> notificationHub,
    INotificationService notificationService) : Hub
{
    private static readonly Dictionary<string, ConnectedUser> Connections = new();
    private static readonly Lock ConnectionsLock = new();

    private string CurrentUserId =>
        Context.UserIdentifier
        ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? Context.User?.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("User is not authenticated.");

    public async Task Join(string roomName)
    {
        ConnectedUser? conn;
        lock (ConnectionsLock)
        {
            Connections.TryGetValue(Context.ConnectionId, out conn);
        }

        if (conn is null) return;

        if (!string.IsNullOrEmpty(conn.CurrentRoom))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conn.CurrentRoom);

        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        lock (ConnectionsLock)
        {
            conn.CurrentRoom = roomName;
        }
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
            PersonId = profileResult.IsSuccess ? profileResult.Value?.PersonId : null,
            IsAdmin = isAdmin,
            ConnectionId = Context.ConnectionId,
            CurrentRoom = string.Empty
        };

        IEnumerable<object> existingUsers;
        lock (ConnectionsLock)
        {
            existingUsers = Connections.Values.Select(BuildUserVm).ToList();
            Connections[Context.ConnectionId] = conn;
        }

        // Send current online users to the new connection
        await Clients.Caller.SendAsync("setOnlineUsers", existingUsers);

        // Notify all other connections of the new user
        await Clients.Others.SendAsync("addUser", BuildUserVm(conn));

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

        if (conn is not null)
            await Clients.Others.SendAsync("removeUser", BuildUserVm(conn));

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendPrivate(string toUserId, string message)
    {
        var result = await chatService.CreatePrivateMessageAsync(CurrentUserId, toUserId, message);
        if (!result.IsSuccess || result.Value is null) return;
        var vm = result.Value;

        // Persist a notification so the recipient sees it even when they were offline
        await notificationService.CreateAsync(vm.Id, CurrentUserId, toUserId);

        // Deliver to all of the recipient's active chat connections
        await Clients.User(toUserId).SendAsync("newPrivateMessage", vm);

        // Echo back to sender
        await Clients.Caller.SendAsync("newPrivateMessage", vm);

        // Push a lightweight notification through the notification hub so the nav badge
        // updates immediately for online users who are not on the chat page
        await notificationHub.Clients.User(toUserId).SendAsync("newPrivateMessage", vm);
    }

    private static object BuildUserVm(ConnectedUser c) => new
    {
        userId = c.UserId,
        userName = c.UserName,
        fullName = c.FullName,
        isAdmin = c.IsAdmin,
        currentRoom = c.CurrentRoom,
        personId = c.PersonId
    };

    private sealed class ConnectedUser
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string? CurrentRoom { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int? PersonId { get; set; }
    }
}
