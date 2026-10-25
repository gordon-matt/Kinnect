namespace Kinnect.Data.Entities;

/// <summary>
/// One way to reach a person: a <see cref="Name"/> such as "Email", "Mobile" or "Facebook", and the
/// <see cref="Value"/> to use, such as the address, number or profile URL.
/// </summary>
public class Contact : BaseEntity<int>
{
    public const int MaxNameLength = 100;

    public const int MaxValueLength = 500;

    public int PersonId { get; set; }

    public string Name { get; set; } = string.Empty;

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
        builder.Property(m => m.Name).IsRequired().HasMaxLength(Contact.MaxNameLength);
        builder.Property(m => m.Value).IsRequired().HasMaxLength(Contact.MaxValueLength);

        builder.HasIndex(m => new { m.PersonId, m.SortOrder });

        builder.HasOne(m => m.Person)
            .WithMany(m => m.Contacts)
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
