namespace Kinnect.Models.Dto;

public class ChatUserDto
{
    public string UserId { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? CurrentRoom { get; set; }

    public int? PersonId { get; set; }
}