using System.Text.Json.Serialization;

namespace Kinnect.Models;

/// <summary>
/// Matches the family-chart library's Datum interface:
/// { id: string, data: { gender: "M"|"F", ... }, rels: { children: [], parents: [], spouses: [] } }
/// </summary>
public class FamilyTreeDatum
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("data")]
    public FamilyTreeData Data { get; set; } = new();

    [JsonPropertyName("rels")]
    public FamilyTreeRels Rels { get; set; } = new();
}

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

public class FamilyTreeRels
{
    [JsonPropertyName("children")]
    public List<string> Children { get; set; } = [];

    [JsonPropertyName("parents")]
    public List<string> Parents { get; set; } = [];

    [JsonPropertyName("spouses")]
    public List<string> Spouses { get; set; } = [];
}