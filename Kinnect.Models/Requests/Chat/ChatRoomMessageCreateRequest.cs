namespace Kinnect.Models.Requests;

public class ChatRoomMessageCreateRequest
{
    public int RoomId { get; set; }

    public string Content { get; set; } = null!;
}