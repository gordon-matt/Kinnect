using System.ComponentModel.DataAnnotations;

namespace Kinnect.Data.Entities;

/// <summary>How the family tree behaves for everyone. More columns can be added here as the tree grows options.</summary>
public class TreeSettings : AppSettings
{
    public const int DefaultMaxGenerations = 8;

    public const int LargestMaxGenerations = 50;

    public override string? ConfigurationSection => "Tree";

    /// <summary>
    /// The most generations of ancestors, and of descendants, a person can ask the tree to show.
    /// Every extra generation makes the tree wider and slower to draw.
    /// </summary>
    [Range(1, LargestMaxGenerations)]
    public int MaxGenerations { get; set; } = DefaultMaxGenerations;
}

public class TreeSettingsMap : IEntityTypeConfiguration<TreeSettings>
{
    public void Configure(EntityTypeBuilder<TreeSettings> builder)
    {
        builder.ToTable("TreeSettings", "app");
        builder.HasKey(m => m.Id);
        builder.Ignore(m => m.ConfigurationSection);
    }
}