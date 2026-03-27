using System.Text.Json.Serialization;

namespace Kinnect.Models.FamilyTree;

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