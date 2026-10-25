using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class PersonServiceTests
{
    [Fact]
    public async Task CreateAsync_InsertsPerson()
    {
        var (options, factory) = InMemoryDb.Create();
        var sut = new PersonService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonVersion>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<PersonLink>(factory),
            new EntityFrameworkRepository<Contact>(factory));

        var result = await sut.CreateAsync(new PersonEditRequest
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            IsMale = false
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Doe", result.Value.FamilyName);
        await using var db = InMemoryDb.CreateContext(options);
        Assert.Equal(1, await db.People.CountAsync());
    }

    [Fact]
    public async Task AddSpouseAsync_SamePerson_ReturnsInvalid()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var p = new Person
        {
            FamilyName = "A",
            GivenNames = "B",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(p);
        await db.SaveChangesAsync();

        var sut = new PersonService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonVersion>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<PersonLink>(factory),
            new EntityFrameworkRepository<Contact>(factory));

        var result = await sut.AddSpouseAsync(p.Id, p.Id);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetSpousesForPersonAsync_ReturnsOtherSpouseDetails()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var a = new Person
        {
            FamilyName = "A",
            GivenNames = "One",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var b = new Person
        {
            FamilyName = "B",
            GivenNames = "Two",
            IsMale = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.AddRange(a, b);
        await db.SaveChangesAsync();

        int low = Math.Min(a.Id, b.Id);
        int high = Math.Max(a.Id, b.Id);
        db.PersonSpouses.Add(new PersonSpouse
        {
            PersonId = low,
            SpouseId = high,
            HasMarriage = true,
            MarriageYear = 2000
        });
        await db.SaveChangesAsync();

        var sut = new PersonService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonVersion>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<PersonLink>(factory),
            new EntityFrameworkRepository<Contact>(factory));

        var result = await sut.GetSpousesForPersonAsync(a.Id);

        Assert.True(result.IsSuccess);
        var spouse = Assert.Single(result.Value);
        Assert.Equal(b.Id, spouse.SpousePersonId);
        Assert.Equal((short)2000, spouse.MarriageYear);
    }

    [Fact]
    public async Task LinkUserAccountAsync_ReturnsConflict_WhenUserAlreadyLinked()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var p1 = new Person
        {
            FamilyName = "X",
            GivenNames = "Y",
            IsMale = true,
            UserId = "user-1",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var p2 = new Person
        {
            FamilyName = "Z",
            GivenNames = "W",
            IsMale = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.AddRange(p1, p2);
        await db.SaveChangesAsync();

        var sut = new PersonService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonVersion>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<PersonLink>(factory),
            new EntityFrameworkRepository<Contact>(factory));

        var result = await sut.LinkUserAccountAsync(p2.Id, "user-1");

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task GetMapPinsAsync_ExcludesDeceasedByDeathEvent()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var alive = new Person
        {
            FamilyName = "A",
            GivenNames = "B",
            IsMale = true,
            Latitude = 1,
            Longitude = 2,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var dead = new Person
        {
            FamilyName = "C",
            GivenNames = "D",
            IsMale = false,
            Latitude = 3,
            Longitude = 4,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.AddRange(alive, dead);
        await db.SaveChangesAsync();

        db.PersonEvents.Add(new PersonEvent
        {
            PersonId = dead.Id,
            EventType = PersonEventType.Death,
            Year = 2020,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new PersonService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonVersion>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<PersonLink>(factory),
            new EntityFrameworkRepository<Contact>(factory));

        var result = await sut.GetMapPinsAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(alive.Id, result.Value.First().PersonId);
    }

    [Fact]
    public async Task UpdateAsync_OwnProfile_ClearsDeceasedAndSavesLinks()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            IsMale = false,
            IsDeceased = true,
            UserId = "user-1",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var sut = new PersonService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonVersion>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<PersonLink>(factory),
            new EntityFrameworkRepository<Contact>(factory));

        var result = await sut.UpdateAsync(person.Id, new PersonEditRequest
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            IsMale = false,
            IsDeceased = true,
            Links =
            [
                new PersonLinkInput { Name = "Site", Url = "https://example.com" },
                new PersonLinkInput { Name = "", Url = "" },
            ]
        }, "user-1");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsDeceased);
        var link = Assert.Single(result.Value.Links);
        Assert.Equal("Site", link.Name);
        Assert.Equal("https://example.com", link.Url);

        await using var assertDb = InMemoryDb.CreateContext(options);
        Assert.False((await assertDb.People.FindAsync(person.Id))!.IsDeceased);
    }

    [Fact]
    public async Task UpdateAsync_RejectsLinkThatIsNotHttp()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            IsMale = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var sut = new PersonService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonVersion>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<PersonLink>(factory),
            new EntityFrameworkRepository<Contact>(factory));

        var result = await sut.UpdateAsync(person.Id, new PersonEditRequest
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            Links = [new PersonLinkInput { Name = "Bad", Url = "javascript:alert(1)" }]
        }, "user-1", isAdmin: true);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }
    [Fact]
    public async Task UpdateAsync_TrimsContactNameAndValueAndDropsEmptyRows()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            IsMale = false,
            UserId = "user-1",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var sut = CreateService(factory);

        var result = await sut.UpdateAsync(person.Id, new PersonEditRequest
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            Contacts =
            [
                new ContactInput { Name = "  Email ", Value = "\tjane@example.com  " },
                new ContactInput { Name = "  ", Value = " " },
                new ContactInput { Name = "Mobile", Value = " 0400 000 000 " },
            ]
        }, "user-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Contacts.Count);
        Assert.Equal("Email", result.Value.Contacts[0].Name);
        Assert.Equal("jane@example.com", result.Value.Contacts[0].Value);
        Assert.Equal("0400 000 000", result.Value.Contacts[1].Value);

        await using var assertDb = InMemoryDb.CreateContext(options);
        var stored = await assertDb.Contacts.OrderBy(c => c.SortOrder).ToListAsync();
        Assert.Equal(["Email", "Mobile"], stored.Select(c => c.Name));
        Assert.Equal(["jane@example.com", "0400 000 000"], stored.Select(c => c.Value));
    }

    [Fact]
    public async Task UpdateAsync_RejectsContactWithoutAValue()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            IsMale = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var result = await CreateService(factory).UpdateAsync(person.Id, new PersonEditRequest
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            Contacts = [new ContactInput { Name = "Email", Value = "   " }]
        }, "user-1", isAdmin: true);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_LeavesContactsAloneWhenNoneAreSent()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            IsMale = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Contacts = [new Contact { Name = "Email", Value = "jane@example.com" }]
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var result = await CreateService(factory).UpdateAsync(person.Id, new PersonEditRequest
        {
            FamilyName = "Doe",
            GivenNames = "Jane"
        }, "user-1", isAdmin: true);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Contacts);
    }

    private static PersonService CreateService(TestDbContextFactory factory) => new(
        new EntityFrameworkRepository<Person>(factory),
        new EntityFrameworkRepository<PersonSpouse>(factory),
        new EntityFrameworkRepository<PersonVersion>(factory),
        new EntityFrameworkRepository<PersonEvent>(factory),
        new EntityFrameworkRepository<PersonLink>(factory),
        new EntityFrameworkRepository<Contact>(factory));
}