namespace Kinnect.Models;

public class GedcomImportResult
{
    public int PeopleImported { get; set; }
    public int PeopleUpdated { get; set; }
    public int RelationshipsImported { get; set; }
    public int EventsImported { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public class LinkUserAccountRequest
{
    public string UserId { get; set; } = null!;
}
