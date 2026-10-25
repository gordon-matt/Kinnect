using System.ComponentModel.DataAnnotations;

namespace Kinnect.Data.Entities;

/// <summary>Which documents can be uploaded, and what happens to them afterwards.</summary>
public class DocumentSettings : AppSettings
{
    public const int MaxAllowedExtensionsLength = 500;

    public override string? ConfigurationSection => "DocumentProcessing";

    /// <summary>Comma-separated list of allowed file extensions, including the leading dot.</summary>
    [Required, StringLength(MaxAllowedExtensionsLength)]
    public string AllowedExtensions { get; set; } = ".pdf,.jpg,.jpeg,.png,.gif,.webp";

    /// <summary>Largest file that can be uploaded, in bytes. Defaults to 5 MB.</summary>
    [Range(1, 524_288_000)]
    public long MaxFileSizeBytes { get; set; } = 5_242_880;

    /// <summary>Pass uploaded images through the photo resize pipeline. Has no effect on PDF files.</summary>
    public bool AutoShrinkDocuments { get; set; } = true;

    public IReadOnlySet<string> AllowedExtensionSet =>
        AllowedExtensions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();
}

public class DocumentSettingsMap : IEntityTypeConfiguration<DocumentSettings>
{
    public void Configure(EntityTypeBuilder<DocumentSettings> builder)
    {
        builder.ToTable("DocumentSettings", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.AllowedExtensions).IsRequired().HasMaxLength(DocumentSettings.MaxAllowedExtensionsLength);
        builder.Ignore(m => m.ConfigurationSection);
        builder.Ignore(m => m.AllowedExtensionSet);
    }
}
