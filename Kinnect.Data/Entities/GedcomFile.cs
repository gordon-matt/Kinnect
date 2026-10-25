namespace Kinnect.Data.Entities;

/// <summary>
/// The GEDCOM file exactly as it was uploaded. It is kept byte-for-byte so a later export can patch Kinnect's
/// changes into it instead of regenerating the file, which would lose every tag Kinnect does not model.
/// The bytes live on disk (<see cref="StoragePath"/>), not in the database, so large files stay cheap on every provider.
/// </summary>
public class GedcomFile : BaseEntity<int>
{
    /// <summary>Name of the uploaded file (the .ged inside the archive when a zip was uploaded).</summary>
    public string FileName { get; set; } = null!;

    /// <summary>Path of the stored original, relative to the file storage base path.</summary>
    public string StoragePath { get; set; } = null!;

    /// <summary>Lowercase hex SHA-256 of the stored bytes, to detect a changed or corrupted original.</summary>
    public string Sha256 { get; set; } = null!;

    public long SizeBytes { get; set; }

    public DateTime ImportedAtUtc { get; set; }
}

public class GedcomFileMap : IEntityTypeConfiguration<GedcomFile>
{
    public void Configure(EntityTypeBuilder<GedcomFile> builder)
    {
        builder.ToTable("GedcomFiles", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.FileName).IsRequired().HasMaxLength(500);
        builder.Property(m => m.StoragePath).IsRequired().HasMaxLength(1000);
        builder.Property(m => m.Sha256).IsRequired().HasMaxLength(64);
    }
}
