using Kinnect.Enums;

namespace Kinnect.Data.Entities;

/// <summary>
/// A person in a <see cref="ParentSet"/>. <see cref="Role"/> keeps which of the two was recorded as the father and
/// which as the mother, so the old father and mother columns migrate without losing anything.
/// </summary>
public class ParentSetMember : BaseEntity<int>
{
    public int ParentSetId { get; set; }

    public int ParentPersonId { get; set; }

    public ParentRole Role { get; set; }

    public virtual ParentSet ParentSet { get; set; } = null!;

    public virtual Person ParentPerson { get; set; } = null!;
}

public class ParentSetMemberMap : IEntityTypeConfiguration<ParentSetMember>
{
    public void Configure(EntityTypeBuilder<ParentSetMember> builder)
    {
        builder.ToTable("ParentSetMembers", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Role).IsRequired();

        builder.HasIndex(m => new { m.ParentSetId, m.ParentPersonId }).IsUnique();
        builder.HasIndex(m => new { m.ParentSetId, m.Role }).IsUnique();
        builder.HasIndex(m => m.ParentPersonId);

        builder.HasOne(m => m.ParentSet)
            .WithMany(m => m.Members)
            .HasForeignKey(m => m.ParentSetId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: the child's own sets already cascade from the person, and SQL Server refuses two
        // cascade paths to one table. Deleting a person removes their memberships first (see PersonService).
        builder.HasOne(m => m.ParentPerson)
            .WithMany(m => m.ParentSetMemberships)
            .HasForeignKey(m => m.ParentPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
