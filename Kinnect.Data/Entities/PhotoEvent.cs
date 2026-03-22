using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class PhotoEvent
{
    public int PhotoId { get; set; }

    public int PersonEventId { get; set; }

    public virtual Photo Photo { get; set; } = null!;

    public virtual PersonEvent PersonEvent { get; set; } = null!;
}

public class PhotoEventMap : IEntityTypeConfiguration<PhotoEvent>
{
    public void Configure(EntityTypeBuilder<PhotoEvent> builder)
    {
        builder.ToTable("PhotoEvents", "app");
        builder.HasKey(m => new { m.PhotoId, m.PersonEventId });

        builder.HasOne(m => m.Photo)
            .WithMany()
            .HasForeignKey(m => m.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.PersonEvent)
            .WithMany()
            .HasForeignKey(m => m.PersonEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
