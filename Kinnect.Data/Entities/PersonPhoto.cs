using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class PersonPhoto : IEntity
{
    public required int PersonId { get; set; }

    public required int PhotoId { get; set; }

    public virtual Person Person { get; set; } = null!;

    public virtual Photo Photo { get; set; } = null!;

    [IgnoreDataMember]
    public object[] KeyValues => [PersonId, PhotoId];
}

public class PersonPhotoMap : IEntityTypeConfiguration<PersonPhoto>
{
    public void Configure(EntityTypeBuilder<PersonPhoto> builder)
    {
        builder.ToTable("PersonPhotos", "app");

        // Composite primary key
        builder.HasKey(m => new { m.PersonId, m.PhotoId });
        builder.Property(m => m.PersonId).IsRequired();
        builder.Property(m => m.PhotoId).IsRequired();

        // Relationships
        builder.HasOne(m => m.Person)
            .WithMany(m => m.PersonPhotos)
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(m => m.Photo)
            .WithMany(m => m.PersonPhotos)
            .HasForeignKey(m => m.Photo)
            .OnDelete(DeleteBehavior.ClientNoAction);
    }
}