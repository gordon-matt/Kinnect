using Kinnect.Enums;

namespace Kinnect.Data.Entities;

/// <summary>
/// One family unit that parented a child: one or two people, and how they relate to the child.
/// A child can have several (birth parents, then adoptive parents), and exactly one is primary.
/// This table replaces the father and mother columns that used to be on <see cref="Person"/>.
/// </summary>
public class ParentSet : BaseEntity<int>
{
    public int ChildPersonId { get; set; }

    public ParentageType Type { get; set; }

    public bool IsPrimary { get; set; }

    /// <summary>When the adoption or the placement happened. Birth sets leave this empty.</summary>
    public short? Year { get; set; }

    public byte? Month { get; set; }

    public byte? Day { get; set; }

    /// <summary>Who may see this set. It is checked on the server before the family tree gets any data.</summary>
    public LinkVisibility Visibility { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Person Child { get; set; } = null!;

    public virtual ICollection<ParentSetMember> Members { get; set; } = [];
}

public class ParentSetMap : IEntityTypeConfiguration<ParentSet>
{
    public void Configure(EntityTypeBuilder<ParentSet> builder)
    {
        builder.ToTable("ParentSets", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).IsRequired();
        builder.Property(m => m.Visibility).IsRequired();

        builder.HasIndex(m => m.ChildPersonId);

        builder.HasOne(m => m.Child)
            .WithMany(m => m.ParentSets)
            .HasForeignKey(m => m.ChildPersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
