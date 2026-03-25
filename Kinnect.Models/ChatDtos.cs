namespace Kinnect.Models;

public class ChatRoomDto
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string AdminUserId { get; set; } = null!;

    public string? AdminUserName { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }

    public string Content { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public string FromUserId { get; set; } = null!;

    public string? FromUserName { get; set; }

    public string? FromFullName { get; set; }

    public int? ToRoomId { get; set; }

    public string? ToRoomName { get; set; }

    public string? ToUserId { get; set; }
}

public class ChatUserDto
{
    public string UserId { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? CurrentRoom { get; set; }
}

public class ChatPrivateConversationTargetDto
{
    public string UserId { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
}

public class ChatDeleteRoomDto
{
    public int RoomId { get; set; }

    public string RoomName { get; set; } = null!;
}

public class ChatDeleteMessageDto
{
    public int MessageId { get; set; }

    public int? RoomId { get; set; }

    public string? RoomName { get; set; }
}
