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

    public ICollection<PersonPhoto> PersonPhotos { get; set; } = [];
}

public class PersonMap : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.FamilyName).IsRequired();
        builder.Property(m => m.GivenNames).IsRequired();
        builder.Property(m => m.IsMale).IsRequired();
    }
}