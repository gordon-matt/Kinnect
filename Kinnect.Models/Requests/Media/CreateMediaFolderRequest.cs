namespace Kinnect.Models.Requests;

public class CreateMediaFolderRequest
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}