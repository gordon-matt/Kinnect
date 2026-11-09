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

    /// <summary>
    /// When set, family names are shown in capitals. The stored name is unchanged, and an edit form shows it as typed.
    /// </summary>
    public bool CapitalizeFamilyNames { get; set; } = true;

    /// <summary>Name order given to a person when nobody has chosen one, including people created by a GEDCOM import.</summary>
    public NameOrder DefaultNameOrder { get; set; } = NameOrder.GivenNamesFirst;
}

/// <summary>Whether the family name or the given names are written first.</summary>
public enum NameOrder
{
    GivenNamesFirst = 0,
    FamilyNameFirst = 1,
}

public class TreeSettingsMap : IEntityTypeConfiguration<TreeSettings>
{
    public void Configure(EntityTypeBuilder<TreeSettings> builder)
    {
        builder.ToTable("TreeSettings", "app");
        builder.HasKey(m => m.Id);
        builder.Ignore(m => m.ConfigurationSection);
        builder.Property(m => m.CapitalizeFamilyNames).HasDefaultValue(true);
        builder.Property(m => m.DefaultNameOrder).HasConversion<int>().HasDefaultValue(NameOrder.GivenNamesFirst);
    }
}