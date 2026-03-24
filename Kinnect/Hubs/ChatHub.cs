using System.Text.RegularExpressions;
using Kinnect.Data;
using Microsoft.AspNetCore.SignalR;

namespace Kinnect.Hubs;

[Authorize]
public partial class ChatHub(ApplicationDbContext dbContext) : Hub
{
    // In-memory presence tracking keyed by ConnectionId
    private static readonly Dictionary<string, ConnectedUser> Connections = new();
    private static readonly Lock ConnectionsLock = new();

    public async Task SendPrivate(string toUserId, string message)
    {
        message = message.Trim();
        if (string.IsNullOrEmpty(message)) return;

        var fromUser = await dbContext.Users.FindAsync(CurrentUserId);
        var toUser = await dbContext.Users.FindAsync(toUserId);
        if (fromUser is null || toUser is null) return;

        var cleanContent = StripHtmlRegex().Replace(message, string.Empty);

        var chatMessage = new ChatMessage
        {
            Content = cleanContent,
            Timestamp = DateTime.UtcNow,
            FromUserId = CurrentUserId,
            ToUserId = toUserId
        };
        dbContext.ChatMessages.Add(chatMessage);
        await dbContext.SaveChangesAsync();

        string fromFullName = GetFullName(CurrentUserId);
        var vm = new
        {
            id = chatMessage.Id,
            content = chatMessage.Content,
            timestamp = chatMessage.Timestamp,
            fromUserId = chatMessage.FromUserId,
            fromUserName = fromUser.UserName,
            fromFullName,
            toUserId = chatMessage.ToUserId
        };

        // Deliver to recipient's active connection and back to sender
        string? recipientConnectionId = GetConnectionId(toUserId);
        if (recipientConnectionId is not null)
            await Clients.Client(recipientConnectionId).SendAsync("newPrivateMessage", vm);

        await Clients.Caller.SendAsync("newPrivateMessage", vm);
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

    public override async Task OnConnectedAsync()
    {
        var user = await dbContext.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            await base.OnConnectedAsync();
            return;
        }

        var conn = new ConnectedUser
        {
            UserId = CurrentUserId,
            UserName = user.UserName ?? CurrentUserId,
            FullName = GetFullName(CurrentUserId),
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

    private string CurrentUserId => Context.UserIdentifier
        ?? throw new InvalidOperationException("User is not authenticated.");

    private string GetFullName(string userId)
    {
        var person = dbContext.People
            .Where(p => p.UserId == userId)
            .Select(p => new { p.GivenNames, p.FamilyName })
            .FirstOrDefault();

        return person is not null
            ? $"{person.GivenNames} {person.FamilyName}".Trim()
            : Context.User?.Identity?.Name ?? userId;
    }

    private static string? GetConnectionId(string userId)
    {
        lock (ConnectionsLock)
        {
            return Connections.FirstOrDefault(kv => kv.Value.UserId == userId).Key;
        }
    }

    private static object BuildUserVm(ConnectedUser c) => new
    {
        userId = c.UserId,
        userName = c.UserName,
        fullName = c.FullName,
        currentRoom = c.CurrentRoom
    };

    private sealed class ConnectedUser
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public string? CurrentRoom { get; set; }
    }

    [GeneratedRegex(@"<.*?>")]
    private static partial Regex StripHtmlRegex();
}
