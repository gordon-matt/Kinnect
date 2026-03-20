using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class Photo : BaseEntity<int>
{
    public string Title { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string? ThumbnailPath { get; set; }

    public string? Description { get; set; }

    public int UploadedByPersonId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Person UploadedBy { get; set; } = null!;

    public virtual ICollection<PersonPhoto> PersonPhotos { get; set; } = [];

    public virtual ICollection<PhotoTag> PhotoTags { get; set; } = [];
}

public class PhotoMap : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("Photos", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Title).IsRequired().HasMaxLength(500);
        builder.Property(m => m.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(m => m.ThumbnailPath).HasMaxLength(1000);

        builder.HasOne(m => m.UploadedBy)
            .WithMany()
            .HasForeignKey(m => m.UploadedByPersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
