using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class PersonServiceTests
{
    [Fact]
    public async Task CreateAsync_InsertsPerson()
    {
        var (options, factory) = SqliteDb.Create();
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
        await using var db = SqliteDb.CreateContext(options);
        Assert.Equal(1, await db.People.CountAsync());
    }

    [Fact]
    public async Task AddSpouseAsync_SamePerson_ReturnsInvalid()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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
    public async Task GetMapPinsAsync_ReturnsOnlyLivingPeopleWithCoordinates()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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
        var deceased = new Person
        {
            FamilyName = "C",
            GivenNames = "D",
            IsMale = false,
            IsDeceased = true,
            Latitude = 3,
            Longitude = 4,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var unplaced = new Person
        {
            FamilyName = "E",
            GivenNames = "F",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.AddRange(alive, deceased, unplaced);
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
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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

        await using var assertDb = SqliteDb.CreateContext(options);
        Assert.False((await assertDb.People.FindAsync(person.Id))!.IsDeceased);
    }

    [Fact]
    public async Task UpdateAsync_RejectsLinkThatIsNotHttp()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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
                new ContactInput { Type = ContactType.Email, Value = "\tjane@example.com  " },
                new ContactInput { Type = ContactType.Phone, Value = " " },
                new ContactInput { Type = ContactType.Mobile, Value = " 0400 000 000 " },
            ]
        }, "user-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Contacts.Count);
        Assert.Equal(ContactType.Email, result.Value.Contacts[0].Type);
        Assert.Equal("jane@example.com", result.Value.Contacts[0].Value);
        Assert.Equal("0400 000 000", result.Value.Contacts[1].Value);

        await using var assertDb = SqliteDb.CreateContext(options);
        var stored = await assertDb.Contacts.OrderBy(c => c.SortOrder).ToListAsync();
        Assert.Equal([ContactType.Email, ContactType.Mobile], stored.Select(c => c.Type));
        Assert.Equal(["jane@example.com", "0400 000 000"], stored.Select(c => c.Value));
    }

    [Fact]
    public async Task UpdateAsync_DropsAContactRowLeftBlank()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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
            Contacts = [new ContactInput { Type = ContactType.Email, Value = "   " }]
        }, "user-1", isAdmin: true);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Contacts);
    }

    [Fact]
    public async Task UpdateAsync_LeavesContactsAloneWhenNoneAreSent()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Doe",
            GivenNames = "Jane",
            IsMale = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Contacts = [new Contact { Type = ContactType.Email, Value = "jane@example.com" }]
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

    [Fact]
    public void Merge_JoinsBioAndNoteWithHeadings()
    {
        Assert.Equal("Only bio", PersonNoteText.Merge("Only bio", null));
        Assert.Equal("Only note", PersonNoteText.Merge("  ", "Only note"));
        Assert.Equal("Bio\nThe life.\n\nNotes\nThe papers.", PersonNoteText.Merge("The life.", "The papers."));
    }

    [Fact]
    public async Task UnlinkUserAccountAsync_AllowsTheSignedInPersonToUnlink()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Kay",
            GivenNames = "Ada",
            UserId = "admin",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var result = await CreateService(factory).UnlinkUserAccountAsync(person.Id, "admin");

        Assert.True(result.IsSuccess);
        await using var assertDb = SqliteDb.CreateContext(options);
        Assert.Null((await assertDb.People.SingleAsync()).UserId);
    }

    private static PersonService CreateService(TestDbContextFactory factory) => new(
        new EntityFrameworkRepository<Person>(factory),
        new EntityFrameworkRepository<PersonSpouse>(factory),
        new EntityFrameworkRepository<PersonVersion>(factory),
        new EntityFrameworkRepository<PersonEvent>(factory),
        new EntityFrameworkRepository<PersonLink>(factory),
        new EntityFrameworkRepository<Contact>(factory));
}