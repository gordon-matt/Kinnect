using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class PersonSpouse : IEntity
{
    public int PersonId { get; set; }

    public int SpouseId { get; set; }

    public short? MarriageYear { get; set; }

    public byte? MarriageMonth { get; set; }

    public byte? MarriageDay { get; set; }

    public short? DivorceYear { get; set; }

    public byte? DivorceMonth { get; set; }

    public byte? DivorceDay { get; set; }

    public virtual Person Person { get; set; } = null!;

    public virtual Person Spouse { get; set; } = null!;

    [IgnoreDataMember]
    public object[] KeyValues => [PersonId, SpouseId];
}

public class PersonSpouseMap : IEntityTypeConfiguration<PersonSpouse>
{
    public void Configure(EntityTypeBuilder<PersonSpouse> builder)
    {
        builder.ToTable("PersonSpouses", "app");
        builder.HasKey(m => new { m.PersonId, m.SpouseId });

        builder.HasOne(m => m.Person)
            .WithMany(m => m.Spouses)
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Spouse)
            .WithMany()
            .HasForeignKey(m => m.SpouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}