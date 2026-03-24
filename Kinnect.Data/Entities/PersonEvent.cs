namespace Kinnect.Data.Entities;

public class PersonEvent : BaseEntity<int>
{
    public int PersonId { get; set; }

    /// <summary>GEDCOM event tag, e.g. BIRT, DEAT, MARR, EMIG, OCCU …</summary>
    public string EventType { get; set; } = null!;

    public short? Year { get; set; }

    public byte? Month { get; set; }

    public byte? Day { get; set; }

    public string? Place { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Description { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Person Person { get; set; } = null!;
}

public class PersonEventMap : IEntityTypeConfiguration<PersonEvent>
{
    public void Configure(EntityTypeBuilder<PersonEvent> builder)
    {
        builder.ToTable("PersonEvents", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.EventType).IsRequired().HasMaxLength(20);
        builder.Property(m => m.Place).HasMaxLength(500);
        builder.Property(m => m.Description).HasMaxLength(2000);
        builder.Property(m => m.Note).HasMaxLength(4000);

        builder.HasOne(m => m.Person)
            .WithMany(m => m.Events)
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}