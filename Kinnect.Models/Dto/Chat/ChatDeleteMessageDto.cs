namespace Kinnect.Models.Dto;

public class ChatDeleteMessageDto
{
    public int MessageId { get; set; }

    public int? RoomId { get; set; }

    public string? RoomName { get; set; }
}