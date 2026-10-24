namespace Kinnect.Data.Entities;

public class PersonLink : BaseEntity<int>
{
    public const int MaxNameLength = 200;

    public const int MaxUrlLength = 2000;

    public int PersonId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public virtual Person Person { get; set; } = null!;
}

public class PersonLinkMap : IEntityTypeConfiguration<PersonLink>
{
    public void Configure(EntityTypeBuilder<PersonLink> builder)
    {
        builder.ToTable("PersonLinks", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(PersonLink.MaxNameLength);
        builder.Property(m => m.Url).IsRequired().HasMaxLength(PersonLink.MaxUrlLength).IsUnicode(false);

        builder.HasIndex(m => new { m.PersonId, m.SortOrder });

        builder.HasOne(m => m.Person)
            .WithMany(m => m.Links)
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
