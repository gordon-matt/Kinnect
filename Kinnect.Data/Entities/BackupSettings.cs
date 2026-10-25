using System.ComponentModel.DataAnnotations;

namespace Kinnect.Data.Entities;

/// <summary>How many person-tree backup files to keep.</summary>
public class BackupSettings : AppSettings
{
    public const int DefaultKeepCount = 8;

    public override string? ConfigurationSection => "Backup";

    /// <summary>Newest files to keep. Zero keeps every backup.</summary>
    [Range(0, 100)]
    public int KeepCount { get; set; } = DefaultKeepCount;
}

public class BackupSettingsMap : IEntityTypeConfiguration<BackupSettings>
{
    public void Configure(EntityTypeBuilder<BackupSettings> builder)
    {
        builder.ToTable("BackupSettings", "app");
        builder.HasKey(m => m.Id);
        builder.Ignore(m => m.ConfigurationSection);
    }
}
