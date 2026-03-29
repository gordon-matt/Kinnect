using System.Text.RegularExpressions;

namespace Kinnect.Services;

public partial class ChatService(
    IRepository<ChatRoom> chatRoomRepository,
    IRepository<ChatMessage> chatMessageRepository,
    IRepository<Person> personRepository,
    IUserInfoService userInfoService) : IChatService
{
    public async Task<Result<ChatMessageDto>> CreatePrivateMessageAsync(string currentUserId, string toUserId, string content)
    {
        string cleanContent = SanitizeMessage(content);
        if (string.IsNullOrWhiteSpace(cleanContent))
        {
            return Result.Invalid(new ValidationError("Message content is required."));
        }

        var userInfo = await userInfoService.GetUserInfoAsync([currentUserId, toUserId]);
        if (!userInfo.ContainsKey(toUserId))
        {
            return Result.NotFound("Recipient user not found.");
        }

        var message = await chatMessageRepository.InsertAsync(new ChatMessage
        {
            Content = cleanContent,
            Timestamp = DateTime.UtcNow,
            FromUserId = currentUserId,
            ToUserId = toUserId
        });

        var displayNames = await GetDisplayNamesAsync([currentUserId]);

        return Result.Success(new ChatMessageDto
        {
            Id = message.Id,
            Content = message.Content,
            Timestamp = message.Timestamp,
            FromUserId = message.FromUserId,
            FromUserName = userInfo.GetValueOrDefault(currentUserId)?.Username,
            FromFullName = displayNames.GetValueOrDefault(currentUserId)
                ?? userInfo.GetValueOrDefault(currentUserId)?.Username
                ?? currentUserId,
            ToUserId = message.ToUserId
        });
    }

    public async Task<Result<ChatRoomDto>> CreateRoomAsync(string name, string currentUserId)
    {
        string normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result.Invalid(new ValidationError("Room name is required."));
        }

        bool exists = await chatRoomRepository.ExistsAsync(r => r.Name == normalizedName);
        if (exists)
        {
            return Result.Conflict("A room with that name already exists.");
        }

        var room = await chatRoomRepository.InsertAsync(new ChatRoom
        {
            Name = normalizedName,
            AdminUserId = currentUserId,
            CreatedAtUtc = DateTime.UtcNow
        });

        string? adminUserName = await GetUserNameAsync(currentUserId);
        return Result.Success(new ChatRoomDto
        {
            Id = room.Id,
            Name = room.Name,
            AdminUserId = room.AdminUserId,
            AdminUserName = adminUserName
        });
    }

    public async Task<Result<ChatMessageDto>> CreateRoomMessageAsync(int roomId, string content, string currentUserId, bool isAdmin = false)
    {
        var room = await chatRoomRepository.FindOneAsync(roomId);
        if (room is null)
        {
            return Result.NotFound("Room not found.");
        }

        if (string.Equals(room.Name, Constants.Chat.AnnouncementsRoomName, StringComparison.OrdinalIgnoreCase) && !isAdmin)
        {
            return Result.Forbidden();
        }

        string cleanContent = SanitizeMessage(content);
        if (string.IsNullOrWhiteSpace(cleanContent))
        {
            return Result.Invalid(new ValidationError("Message content is required."));
        }

        var message = await chatMessageRepository.InsertAsync(new ChatMessage
        {
            Content = cleanContent,
            Timestamp = DateTime.UtcNow,
            FromUserId = currentUserId,
            ToRoomId = roomId
        });

        var userInfo = await userInfoService.GetUserInfoAsync([currentUserId]);
        var displayNames = await GetDisplayNamesAsync([currentUserId]);

        return Result.Success(new ChatMessageDto
        {
            Id = message.Id,
            Content = message.Content,
            Timestamp = message.Timestamp,
            FromUserId = message.FromUserId,
            FromUserName = userInfo.GetValueOrDefault(currentUserId)?.Username,
            FromFullName = displayNames.GetValueOrDefault(currentUserId)
                ?? userInfo.GetValueOrDefault(currentUserId)?.Username
                ?? currentUserId,
            ToRoomId = message.ToRoomId,
            ToRoomName = room.Name
        });
    }

    public async Task<Result<ChatDeleteMessageDto>> DeleteMessageAsync(int messageId, string currentUserId)
    {
        var message = await chatMessageRepository.FindOneAsync(messageId);
        if (message is null)
        {
            return Result.NotFound("Message not found.");
        }

        if (!string.Equals(message.FromUserId, currentUserId, StringComparison.Ordinal))
        {
            return Result.Forbidden();
        }

        string? roomName = null;
        if (message.ToRoomId.HasValue)
        {
            roomName = await chatRoomRepository.FindOneAsync(new SearchOptions<ChatRoom>
            {
                Query = x => x.Id == message.ToRoomId.Value
            }, x => x.Name);
        }

        await chatMessageRepository.DeleteAsync(message);
        return Result.Success(new ChatDeleteMessageDto
        {
            MessageId = messageId,
            RoomId = message.ToRoomId,
            RoomName = roomName
        });
    }

    public async Task<Result<ChatDeleteRoomDto>> DeleteRoomAsync(int roomId, string currentUserId, bool isAdmin = false)
    {
        var room = await chatRoomRepository.FindOneAsync(roomId);
        if (room is null)
        {
            return Result.NotFound("Room not found.");
        }

        if (string.Equals(room.Name, Constants.Chat.AnnouncementsRoomName, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Forbidden();
        }

        if (!isAdmin && !string.Equals(room.AdminUserId, currentUserId, StringComparison.Ordinal))
        {
            return Result.Forbidden();
        }

        var result = new ChatDeleteRoomDto
        {
            RoomId = room.Id,
            RoomName = room.Name
        };

        await chatRoomRepository.DeleteAsync(room);
        return Result.Success(result);
    }

    public async Task<Result<ChatUserDto>> GetCurrentChatUserAsync(string currentUserId, string fallbackUserName)
    {
        var userInfo = await userInfoService.GetUserInfoAsync([currentUserId]);
        string userName = userInfo.GetValueOrDefault(currentUserId)?.Username ?? fallbackUserName;

        var people = await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = p => p.UserId == currentUserId
        });
        var person = people.FirstOrDefault();
        string fullName = person is not null
            ? $"{person.GivenNames} {person.FamilyName}".Trim()
            : userName;

        return Result.Success(new ChatUserDto
        {
            UserId = currentUserId,
            UserName = userName,
            FullName = fullName,
            PersonId = person?.Id,
            CurrentRoom = string.Empty
        });
    }

    public async Task<Result<IEnumerable<ChatPrivateConversationTargetDto>>> GetPrivateConversationPartnersAsync(string currentUserId)
    {
        var messages = await chatMessageRepository.FindAsync(new SearchOptions<ChatMessage>
        {
            Query = m => m.ToUserId != null && (m.FromUserId == currentUserId || m.ToUserId == currentUserId)
        });

        var partnerLatest = messages
            .GroupBy(m => m.FromUserId == currentUserId ? m.ToUserId! : m.FromUserId)
            .Select(g => new { PartnerId = g.Key, Latest = g.Max(m => m.Timestamp) })
            .OrderByDescending(x => x.Latest)
            .ToList();

        if (partnerLatest.Count == 0)
            return Result.Success(Enumerable.Empty<ChatPrivateConversationTargetDto>());

        var partnerIds = partnerLatest.Select(x => x.PartnerId).ToList();
        var userInfo = await userInfoService.GetUserInfoAsync(partnerIds);
        var displayNames = await GetDisplayNamesAsync(partnerIds);

        var dtos = partnerLatest.Select(x => new ChatPrivateConversationTargetDto
        {
            UserId = x.PartnerId,
            DisplayName = displayNames.GetValueOrDefault(x.PartnerId)
                ?? userInfo.GetValueOrDefault(x.PartnerId)?.Username
                ?? x.PartnerId
        });

        return Result.Success(dtos);
    }

    public async Task<Result<ChatPrivateConversationTargetDto>> GetPrivateConversationTargetAsync(string userId)
    {
        var userInfo = await userInfoService.GetUserInfoAsync([userId]);
        if (!userInfo.ContainsKey(userId))
        {
            return Result.NotFound("User not found.");
        }

        var displayNames = await GetDisplayNamesAsync([userId]);
        string displayName = displayNames.GetValueOrDefault(userId)
            ?? userInfo[userId].Username
            ?? userId;

        return Result.Success(new ChatPrivateConversationTargetDto
        {
            UserId = userId,
            DisplayName = displayName
        });
    }

    public async Task<Result<IEnumerable<ChatMessageDto>>> GetPrivateMessagesAsync(string currentUserId, string otherUserId, int take)
    {
        int takeCount = Math.Clamp(take, 1, 200);

        var messages = await chatMessageRepository.FindAsync(new SearchOptions<ChatMessage>
        {
            Query = m => m.ToUserId != null &&
                (
                    (m.FromUserId == currentUserId && m.ToUserId == otherUserId) ||
                    (m.FromUserId == otherUserId && m.ToUserId == currentUserId)
                ),

            OrderBy = query => query.OrderByDescending(m => m.Timestamp),
            PageNumber = 1,
            PageSize = takeCount
        });

        var ordered = messages
            .OrderBy(m => m.Timestamp)
            .ToList();

        return Result.Success(await MapMessagesAsync(ordered));
    }

    public async Task<Result<ChatRoomDto>> GetRoomByIdAsync(int roomId)
    {
        var room = await chatRoomRepository.FindOneAsync(roomId);
        if (room is null)
        {
            return Result.NotFound("Room not found.");
        }

        string? adminUserName = await GetUserNameAsync(room.AdminUserId);
        return Result.Success(new ChatRoomDto
        {
            Id = room.Id,
            Name = room.Name,
            AdminUserId = room.AdminUserId,
            AdminUserName = adminUserName
        });
    }

    public async Task<Result<IEnumerable<ChatMessageDto>>> GetRoomMessagesAsync(int roomId, int take)
    {
        int takeCount = Math.Clamp(take, 1, 200);

        var room = await chatRoomRepository.FindOneAsync(roomId);
        if (room is null)
        {
            return Result.NotFound("Room not found.");
        }

        var messages = await chatMessageRepository.FindAsync(new SearchOptions<ChatMessage>
        {
            Query = m => m.ToRoomId == roomId,
            OrderBy = query => query.OrderByDescending(m => m.Timestamp),
            PageNumber = 1,
            PageSize = takeCount
        });

        var ordered = messages
            .OrderBy(m => m.Timestamp)
            .ToList();

        return Result.Success(await MapMessagesAsync(ordered, room.Name));
    }

    public async Task<Result<IEnumerable<ChatRoomDto>>> GetRoomsAsync()
    {
        var rooms = (await chatRoomRepository.FindAsync(new SearchOptions<ChatRoom>
        {
            OrderBy = query => query
                .OrderByDescending(r => r.Name == Constants.Chat.AnnouncementsRoomName)
                .ThenBy(r => r.Name)
        })).ToList();

        var adminIds = rooms.Select(r => r.AdminUserId).Distinct().ToList();
        var userInfo = await userInfoService.GetUserInfoAsync(adminIds);

        var dtos = rooms.Select(room => new ChatRoomDto
        {
            Id = room.Id,
            Name = room.Name,
            AdminUserId = room.AdminUserId,
            AdminUserName = userInfo.GetValueOrDefault(room.AdminUserId)?.Username
        });

        return Result.Success(dtos);
    }

    public async Task<Result<ChatRoomDto>> UpdateRoomAsync(int roomId, string name, string currentUserId)
    {
        string normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result.Invalid(new ValidationError("Room name is required."));
        }

        var room = await chatRoomRepository.FindOneAsync(roomId);
        if (room is null)
        {
            return Result.NotFound("Room not found.");
        }

        if (string.Equals(room.Name, Constants.Chat.AnnouncementsRoomName, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Forbidden();
        }

        if (!string.Equals(room.AdminUserId, currentUserId, StringComparison.Ordinal))
        {
            return Result.Forbidden();
        }

        bool nameConflict = await chatRoomRepository.ExistsAsync(r => r.Name == normalizedName && r.Id != roomId);
        if (nameConflict)
        {
            return Result.Conflict("A room with that name already exists.");
        }

        room.Name = normalizedName;
        await chatRoomRepository.UpdateAsync(room);

        string? adminUserName = await GetUserNameAsync(room.AdminUserId);
        return Result.Success(new ChatRoomDto
        {
            Id = room.Id,
            Name = room.Name,
            AdminUserId = room.AdminUserId,
            AdminUserName = adminUserName
        });
    }

    private static string SanitizeMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        return StripHtmlRegex().Replace(content, string.Empty).Trim();
    }

    [GeneratedRegex(@"<.*?>")]
    private static partial Regex StripHtmlRegex();

    private async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var people = await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = p => p.UserId != null && ids.Contains(p.UserId)
        });

        return people
            .Where(p => p.UserId != null)
            .GroupBy(p => p.UserId!)
            .ToDictionary(
                g => g.Key,
                g => $"{g.First().GivenNames} {g.First().FamilyName}".Trim());
    }

    private async Task<string?> GetUserNameAsync(string userId)
    {
        var userInfo = await userInfoService.GetUserInfoAsync([userId]);
        return userInfo.GetValueOrDefault(userId)?.Username;
    }

    private async Task<IEnumerable<ChatMessageDto>> MapMessagesAsync(IEnumerable<ChatMessage> messages, string? roomName = null)
    {
        var list = messages.ToList();
        var fromUserIds = list.Select(m => m.FromUserId).Distinct().ToList();

        var userInfo = await userInfoService.GetUserInfoAsync(fromUserIds);
        var displayNames = await GetDisplayNamesAsync(fromUserIds);

        return list.Select(m =>
        {
            string? userName = userInfo.GetValueOrDefault(m.FromUserId)?.Username;
            string? fullName = displayNames.GetValueOrDefault(m.FromUserId) ?? userName ?? m.FromUserId;
            return new ChatMessageDto
            {
                Id = m.Id,
                Content = m.Content,
                Timestamp = m.Timestamp,
                FromUserId = m.FromUserId,
                FromUserName = userName,
                FromFullName = fullName,
                ToRoomId = m.ToRoomId,
                ToRoomName = roomName,
                ToUserId = m.ToUserId
            };
        });
    }
}