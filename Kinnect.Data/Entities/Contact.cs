using Kinnect.Enums;

namespace Kinnect.Data.Entities;

/// <summary>
/// One way to reach a person. <see cref="Type"/> says which GEDCOM tag it exports as, and
/// <see cref="Value"/> is the address, number or URL.
/// </summary>
public class Contact : BaseEntity<int>
{
    public const int MaxValueLength = 500;

    public int PersonId { get; set; }

    public ContactType Type { get; set; }

    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public virtual Person Person { get; set; } = null!;
}

public class ContactMap : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contacts", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).IsRequired();
        builder.Property(m => m.Value).IsRequired().HasMaxLength(Contact.MaxValueLength);

        builder.HasIndex(m => new { m.PersonId, m.SortOrder });

        builder.HasOne(m => m.Person)
            .WithMany(m => m.Contacts)
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
