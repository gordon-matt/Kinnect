namespace Kinnect.Data.Entities;

public class Document : BaseEntity<int>
{
    public string Title { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string? Description { get; set; }

    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }

    /// <summary>The <c>FILE</c> value from an imported GEDCOM. Null for documents uploaded in Kinnect.</summary>
    public string? GedcomFileReference { get; set; }

    public int UploadedByPersonId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Person UploadedBy { get; set; } = null!;

    public virtual ICollection<DocumentTag> DocumentTags { get; set; } = [];
}

public class DocumentMap : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Title).IsRequired().HasMaxLength(500);
        builder.Property(m => m.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(m => m.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(m => m.GedcomFileReference).HasMaxLength(1000);

        builder.HasOne(m => m.UploadedBy)
            .WithMany()
            .HasForeignKey(m => m.UploadedByPersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}