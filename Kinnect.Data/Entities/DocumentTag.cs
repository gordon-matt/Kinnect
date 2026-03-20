using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class DocumentTag : IEntity
{
    public required int DocumentId { get; set; }

    public required int TagId { get; set; }

    public virtual Document Document { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;

    [IgnoreDataMember]
    public object[] KeyValues => [DocumentId, TagId];
}

public class DocumentTagMap : IEntityTypeConfiguration<DocumentTag>
{
    public void Configure(EntityTypeBuilder<DocumentTag> builder)
    {
        builder.ToTable("DocumentTags", "app");
        builder.HasKey(m => new { m.DocumentId, m.TagId });

        builder.HasOne(m => m.Document)
            .WithMany(m => m.DocumentTags)
            .HasForeignKey(m => m.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Tag)
            .WithMany(m => m.DocumentTags)
            .HasForeignKey(m => m.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
