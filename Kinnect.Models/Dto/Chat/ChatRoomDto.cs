namespace Kinnect.Models.Dto;

public class ChatRoomDto
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string AdminUserId { get; set; } = null!;

    public string? AdminUserName { get; set; }
}