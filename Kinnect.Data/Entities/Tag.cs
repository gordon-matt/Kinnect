using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class Tag : BaseEntity<int>
{
    public required string Name { get; set; }

    public ICollection<PhotoTag> PhotoTags { get; set; } = [];
}

public class TagMap : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired();
    }
}