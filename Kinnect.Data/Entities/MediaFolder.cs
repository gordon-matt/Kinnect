using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class MediaFolder : BaseEntity<int>
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int CreatedByPersonId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Person CreatedBy { get; set; } = null!;

    public virtual ICollection<Photo> Photos { get; set; } = [];

    public virtual ICollection<Video> Videos { get; set; } = [];
}

public class MediaFolderMap : IEntityTypeConfiguration<MediaFolder>
{
    public void Configure(EntityTypeBuilder<MediaFolder> builder)
    {
        builder.ToTable("MediaFolders", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);

        builder.HasOne(m => m.CreatedBy)
            .WithMany()
            .HasForeignKey(m => m.CreatedByPersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
