using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class PhotoTag : IEntity
{
    public required int PhotoId { get; set; }

    public required int TagId { get; set; }

    public virtual Photo Photo { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;

    [IgnoreDataMember]
    public object[] KeyValues => [PhotoId, TagId];
}

public class PhotoTagMap : IEntityTypeConfiguration<PhotoTag>
{
    public void Configure(EntityTypeBuilder<PhotoTag> builder)
    {
        builder.ToTable("PhotoTags", "app");
        builder.HasKey(m => new { m.PhotoId, m.TagId });

        builder.HasOne(m => m.Photo)
            .WithMany(m => m.PhotoTags)
            .HasForeignKey(m => m.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Tag)
            .WithMany(m => m.PhotoTags)
            .HasForeignKey(m => m.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}