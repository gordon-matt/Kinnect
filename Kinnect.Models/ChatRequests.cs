namespace Kinnect.Models;

public class ChatRoomUpsertRequest
{
    public string Name { get; set; } = null!;
}

public class ChatRoomMessageCreateRequest
{
    public int RoomId { get; set; }

    public string Content { get; set; } = null!;
}
