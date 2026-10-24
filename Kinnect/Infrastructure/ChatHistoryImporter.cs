using EasyChatz.Data;
using Kinnect.Data;
using Microsoft.EntityFrameworkCore;
using ChatMessage = EasyChatz.Data.Entities.ChatMessage;
using ChatRoom = EasyChatz.Data.Entities.ChatRoom;
using MessageNotification = EasyChatz.Data.Entities.MessageNotification;

namespace Kinnect.Infrastructure;

/// <summary>
/// Copies Kinnect's original chat rows into EasyChatz once, when EasyChatz has no messages yet.
/// Later Kinnect rows are left where they are; this is a one-time move for an existing database.
/// </summary>
public sealed class ChatHistoryImporter(
    ILogger<ChatHistoryImporter> logger,
    IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var kinnect = scope.ServiceProvider.GetRequiredService<ApplicationDbContextBase>();
            var chats = scope.ServiceProvider.GetRequiredService<IEasyChatzDbContextFactory>();

            using var chatContext = (EasyChatzDbContextBase)chats.GetContext();
            if (await chatContext.Messages.AnyAsync(cancellationToken))
            {
                return;
            }

            if (!await kinnect.ChatMessages.AnyAsync(cancellationToken))
            {
                return;
            }

            var rooms = await kinnect.ChatRooms.AsNoTracking().ToListAsync(cancellationToken);
            var roomIds = new Dictionary<int, int>();
            foreach (var room in rooms)
            {
                var existing = await chatContext.Rooms.FirstOrDefaultAsync(r => r.Name == room.Name, cancellationToken);
                if (existing is null)
                {
                    existing = new ChatRoom
                    {
                        Name = room.Name,
                        AdminUserId = room.AdminUserId,
                        CreatedAtUtc = room.CreatedAtUtc,
                    };
                    chatContext.Rooms.Add(existing);
                    await chatContext.SaveChangesAsync(cancellationToken);
                }

                roomIds[room.Id] = existing.Id;
            }

            var messages = await kinnect.ChatMessages.AsNoTracking().OrderBy(m => m.Id).ToListAsync(cancellationToken);
            var messageIds = new Dictionary<int, int>();
            foreach (var message in messages)
            {
                int? roomId = message.ToRoomId is int oldRoom && roomIds.TryGetValue(oldRoom, out int mapped)
                    ? mapped
                    : null;
                var copy = new ChatMessage
                {
                    Content = message.Content,
                    Timestamp = message.Timestamp,
                    FromUserId = message.FromUserId,
                    ToRoomId = roomId,
                    ToUserId = message.ToUserId,
                };
                chatContext.Messages.Add(copy);
                await chatContext.SaveChangesAsync(cancellationToken);
                messageIds[message.Id] = copy.Id;
            }

            var notices = await kinnect.MessageNotifications.AsNoTracking().ToListAsync(cancellationToken);
            foreach (var notice in notices)
            {
                if (!messageIds.TryGetValue(notice.ChatMessageId, out int messageId))
                {
                    continue;
                }

                chatContext.Notifications.Add(new MessageNotification
                {
                    ChatMessageId = messageId,
                    FromUserId = notice.FromUserId,
                    ToUserId = notice.ToUserId,
                    IsRead = notice.IsRead,
                    DeliveredAtUtc = notice.EmailSent ? notice.CreatedAtUtc : null,
                    CreatedAtUtc = notice.CreatedAtUtc,
                });
            }

            await chatContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Copied {Count} Kinnect chat messages into EasyChatz.", messages.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not copy existing Kinnect chat into EasyChatz.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
