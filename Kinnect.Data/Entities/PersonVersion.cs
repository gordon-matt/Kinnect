namespace Kinnect.Data.Entities;

public class PersonVersion : BaseEntity<int>
{
    public int PersonId { get; set; }

    public string VersionData { get; set; } = null!;

    public string? ChangedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Person Person { get; set; } = null!;
}

public class PersonVersionMap : IEntityTypeConfiguration<PersonVersion>
{
    public void Configure(EntityTypeBuilder<PersonVersion> builder)
    {
        builder.ToTable("PersonVersions", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.VersionData).IsRequired();

        builder.HasOne(m => m.Person)
            .WithMany(m => m.Versions)
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}