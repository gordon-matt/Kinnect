using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class Person : BaseEntity<int>
{
    public string? UserId { get; set; }

    public string FamilyName { get; set; } = null!;

    public string GivenNames { get; set; } = null!;

    public bool IsMale { get; set; }

    public short? YearOfBirth { get; set; }

    public byte? MonthOfBirth { get; set; }

    public byte? DayOfBirth { get; set; }

    public short? YearOfDeath { get; set; }

    public byte? MonthOfDeath { get; set; }

    public byte? DayOfDeath { get; set; }

    public string? PlaceOfBirth { get; set; }

    public string? PlaceOfDeath { get; set; }

    public string? Bio { get; set; }

    public string? ProfileImagePath { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? FatherId { get; set; }

    public int? MotherId { get; set; }

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