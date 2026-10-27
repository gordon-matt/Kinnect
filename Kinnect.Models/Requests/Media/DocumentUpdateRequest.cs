namespace Kinnect.Models.Requests;

public class DocumentUpdateRequest
{
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public List<string>? Tags { get; set; }
}