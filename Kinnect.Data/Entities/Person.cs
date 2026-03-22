using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class Person : BaseEntity<int>
{
    public string? UserId { get; set; }

    public string FamilyName { get; set; } = null!;

    public string GivenNames { get; set; } = null!;

    public bool IsMale { get; set; }

    public string? PlaceOfBirth { get; set; }

    public string? PlaceOfDeath { get; set; }

    public string? Bio { get; set; }

    public string? ProfileImagePath { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? FatherId { get; set; }

    public int? MotherId { get; set; }

    public string? Occupation { get; set; }

    public string? Education { get; set; }

    public string? Religion { get; set; }

    public string? Note { get; set; }

    /// <summary>GEDCOM cross-reference ID used during import (e.g. @I1@).</summary>
    public string? GedcomId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual ApplicationUser? User { get; set; }

    public virtual Person? Father { get; set; }

    public virtual Person? Mother { get; set; }

    public virtual ICollection<Person> ChildrenAsFather { get; set; } = [];

    public virtual ICollection<Person> ChildrenAsMother { get; set; } = [];

    public virtual ICollection<PersonSpouse> Spouses { get; set; } = [];

    public virtual ICollection<PersonVersion> Versions { get; set; } = [];

    public virtual ICollection<Post> Posts { get; set; } = [];

    public virtual ICollection<PersonPhoto> PersonPhotos { get; set; } = [];

    public virtual ICollection<PersonEvent> Events { get; set; } = [];
}

public class PersonMap : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.FamilyName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.GivenNames).IsRequired().HasMaxLength(200);
        builder.Property(m => m.IsMale).IsRequired();
        builder.Property(m => m.PlaceOfBirth).HasMaxLength(500);
        builder.Property(m => m.PlaceOfDeath).HasMaxLength(500);
        builder.Property(m => m.ProfileImagePath).HasMaxLength(500);
        builder.Property(m => m.Occupation).HasMaxLength(500);
        builder.Property(m => m.Education).HasMaxLength(500);
        builder.Property(m => m.Religion).HasMaxLength(200);
        builder.Property(m => m.Note).HasMaxLength(4000);
        builder.Property(m => m.GedcomId).HasMaxLength(50);

        builder.HasOne(m => m.User)
            .WithOne()
            .HasForeignKey<Person>(m => m.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.Father)
            .WithMany(m => m.ChildrenAsFather)
            .HasForeignKey(m => m.FatherId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.Mother)
            .WithMany(m => m.ChildrenAsMother)
            .HasForeignKey(m => m.MotherId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
    }
}