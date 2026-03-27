namespace Kinnect.Models.Dto;

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