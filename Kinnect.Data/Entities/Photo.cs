using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class Photo : BaseEntity<int>
{
    public string Title { get; set; } = null!;

    public string Uri { get; set; } = null!;

    public string ThumbnailUri { get; set; } = null!;

    public string? Description { get; set; }

    // TODO: Add EXIF metadata properties (e.g. DateTaken, Location, etc.)

    public ICollection<PersonPhoto> PersonPhotos { get; set; } = [];

    public ICollection<PhotoTag> PhotoTags { get; set; } = [];
}

public class PhotoMap : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("Photos", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Title).IsRequired();
        builder.Property(m => m.Uri).IsRequired();
        builder.Property(m => m.ThumbnailUri).IsRequired();
    }
}