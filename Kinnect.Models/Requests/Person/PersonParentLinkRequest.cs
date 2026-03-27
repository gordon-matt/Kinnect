namespace Kinnect.Models.Requests;

public class PersonParentLinkRequest
{
    public int? FatherId { get; set; }

    public int? MotherId { get; set; }
}