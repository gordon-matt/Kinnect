using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class GedcomServiceTests
{
    [Fact]
    public async Task ExportAsync_ContainsIndividualAndBirth()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Doe",
            GivenNames = "John",
            IsMale = true,
            GedcomId = "@I1@",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        db.PersonEvents.Add(new PersonEvent
        {
            PersonId = person.Id,
            EventType = PersonEventType.Birth,
            Year = 1980,
            Month = 5,
            Day = 15,
            Place = "London",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new GedcomService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory));

        string gedcom = await sut.ExportAsync();

        Assert.Contains("0 HEAD", gedcom, StringComparison.Ordinal);
        Assert.Contains("INDI", gedcom, StringComparison.Ordinal);
        Assert.Contains("John", gedcom, StringComparison.Ordinal);
        Assert.Contains("Doe", gedcom, StringComparison.Ordinal);
        Assert.Contains("BIRT", gedcom, StringComparison.Ordinal);
        Assert.Contains("London", gedcom, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_MinimalFile_ImportsIndividual()
    {
        var (options, factory) = InMemoryDb.Create();
        var sut = new GedcomService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory));

        string ged = """
            0 HEAD
            1 GEDC
            2 VERS 5.5.1
            0 @I1@ INDI
            1 NAME Jane /Roe/
            1 SEX F
            0 TRLR
            """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ged));
        var result = await sut.ImportAsync(stream);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.PeopleImported > 0 || result.Value.PeopleUpdated > 0);
        await using var db = InMemoryDb.CreateContext(options);
        Assert.NotEmpty(await db.People.ToListAsync());
    }
}