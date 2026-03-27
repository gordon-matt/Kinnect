using System.Text.Json.Serialization;

namespace Kinnect.Models.FamilyTree;

public class FamilyTreeRels
{
    [JsonPropertyName("children")]
    public List<string> Children { get; set; } = [];

    [JsonPropertyName("parents")]
    public List<string> Parents { get; set; } = [];

    [JsonPropertyName("spouses")]
    public List<string> Spouses { get; set; } = [];
}