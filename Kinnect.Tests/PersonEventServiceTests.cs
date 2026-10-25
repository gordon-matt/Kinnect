using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class PersonEventServiceTests
{
    [Fact]
    public async Task CreateAsync_SecondBirth_ReturnsInvalid()
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

        db.PersonEvents.Add(new PersonEvent
        {
            PersonId = person.Id,
            EventType = PersonEventType.Birth,
            Year = 1990,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new PersonEventService(
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory));
        var request = new PersonEventRequest { EventType = PersonEventType.Birth, Year = 1991 };

        var result = await sut.CreateAsync(person.Id, request);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task CreateAsync_MarriageType_ReturnsInvalid_NotOnTimeline()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Doe",
            GivenNames = "John",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var sut = new PersonEventService(
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory));
        var request = new PersonEventRequest { EventType = PersonEventType.Marriage };

        var result = await sut.CreateAsync(person.Id, request);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetByPersonAsync_ExcludesNonTimelineTypes()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "X",
            GivenNames = "Y",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        db.PersonEvents.AddRange(
            new PersonEvent
            {
                PersonId = person.Id,
                EventType = PersonEventType.Birth,
                Year = 2000,
                CreatedAtUtc = DateTime.UtcNow
            },
            new PersonEvent
            {
                PersonId = person.Id,
                EventType = PersonEventType.Marriage,
                Year = 2010,
                CreatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var sut = new PersonEventService(
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory));

        var result = await sut.GetByPersonAsync(person.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(PersonEventType.Birth, result.Value.First().EventType);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEvent()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "A",
            GivenNames = "B",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var evt = new PersonEvent
        {
            PersonId = person.Id,
            EventType = PersonEventType.Residence,
            Year = 2020,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.PersonEvents.Add(evt);
        await db.SaveChangesAsync();

        var sut = new PersonEventService(
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory));

        var del = await sut.DeleteAsync(evt.Id);

        Assert.True(del.IsSuccess);
        await using var assertDb = SqliteDb.CreateContext(options);
        Assert.Null(await assertDb.PersonEvents.FindAsync(evt.Id));
    }

    [Fact]
    public async Task CopyToCloseRelativesAsync_CopiesResidenceToSpouseChildAndSibling()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var father = Person("Adam", "Ward", true);
        var mother = Person("Eve", "Ward", false);
        db.People.AddRange(father, mother);
        await db.SaveChangesAsync();

        var child = Person("Jane", "Ward", false);
        var sibling = Person("John", "Ward", true);
        child.FatherId = father.Id;
        child.MotherId = mother.Id;
        sibling.FatherId = father.Id;
        sibling.MotherId = mother.Id;
        var spouse = Person("Sam", "Ward", true);
        var stranger = Person("Pat", "Other", false);
        db.People.AddRange(child, sibling, spouse, stranger);
        await db.SaveChangesAsync();

        int low = Math.Min(child.Id, spouse.Id);
        int high = Math.Max(child.Id, spouse.Id);
        db.PersonSpouses.Add(new PersonSpouse { PersonId = low, SpouseId = high });
        var residence = new PersonEvent
        {
            PersonId = child.Id,
            EventType = PersonEventType.Residence,
            Year = 1990,
            Place = "Adelaide",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.PersonEvents.Add(residence);
        await db.SaveChangesAsync();

        var sut = new PersonEventService(
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory));

        var birth = await AddBirth(db, child.Id);
        var birthCopy = await sut.CopyToCloseRelativesAsync(birth.Id, [spouse.Id], "editor", isAdmin: true);
        Assert.Equal(ResultStatus.Invalid, birthCopy.Status);

        var copied = await sut.CopyToCloseRelativesAsync(
            residence.Id,
            [spouse.Id, sibling.Id, father.Id, stranger.Id],
            "editor",
            isAdmin: true);

        Assert.True(copied.IsSuccess);
        Assert.Equal(3, copied.Value);

        await using var assertDb = SqliteDb.CreateContext(options);
        var places = await assertDb.PersonEvents
            .Where(evt => evt.EventType == PersonEventType.Residence)
            .Select(evt => evt.PersonId)
            .ToListAsync();
        Assert.Contains(child.Id, places);
        Assert.Contains(spouse.Id, places);
        Assert.Contains(sibling.Id, places);
        Assert.Contains(father.Id, places);
        Assert.DoesNotContain(stranger.Id, places);
        Assert.DoesNotContain(mother.Id, places);

        var again = await sut.CopyToCloseRelativesAsync(residence.Id, [spouse.Id], "editor", isAdmin: true);
        Assert.True(again.IsSuccess);
        Assert.Equal(0, again.Value);
    }

    [Fact]
    public async Task CopyToCloseRelativesAsync_RefusesRelativeOwnedBySomeoneElse()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var me = Person("Jane", "Ward", false);
        me.UserId = "me";
        var spouse = Person("Sam", "Ward", true);
        spouse.UserId = "sam";
        db.People.AddRange(me, spouse);
        await db.SaveChangesAsync();
        db.PersonSpouses.Add(new PersonSpouse
        {
            PersonId = Math.Min(me.Id, spouse.Id),
            SpouseId = Math.Max(me.Id, spouse.Id)
        });
        var residence = new PersonEvent
        {
            PersonId = me.Id,
            EventType = PersonEventType.Residence,
            Year = 2001,
            Place = "Hobart",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.PersonEvents.Add(residence);
        await db.SaveChangesAsync();

        var sut = new PersonEventService(
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory));

        var result = await sut.CopyToCloseRelativesAsync(residence.Id, [spouse.Id], "me");

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    private static Person Person(string given, string family, bool isMale) => new()
    {
        GivenNames = given,
        FamilyName = family,
        IsMale = isMale,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private static async Task<PersonEvent> AddBirth(ApplicationDbContext db, int personId)
    {
        var birth = new PersonEvent
        {
            PersonId = personId,
            EventType = PersonEventType.Birth,
            Year = 1970,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.PersonEvents.Add(birth);
        await db.SaveChangesAsync();
        return birth;
    }
}