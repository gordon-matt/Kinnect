using System.Text.Json.Serialization;

namespace Kinnect.Models.FamilyTree;

public class FamilyTreeData
{
    [JsonPropertyName("gender")]
    public string Gender { get; set; } = "M";

    [JsonPropertyName("first name")]
    public string FirstName { get; set; } = null!;

    [JsonPropertyName("last name")]
    public string LastName { get; set; } = null!;

    [JsonPropertyName("birthday")]
    public string? Birthday { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("personId")]
    public int PersonId { get; set; }

    [JsonPropertyName("hasAccount")]
    public bool HasAccount { get; set; }
}