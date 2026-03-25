using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class PersonEventServiceTests
{
    [Fact]
    public async Task CreateAsync_SecondBirth_ReturnsInvalid()
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

        db.PersonEvents.Add(new PersonEvent
        {
            PersonId = person.Id,
            EventType = PersonEventType.Birth,
            Year = 1990,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new PersonEventService(new EntityFrameworkRepository<PersonEvent>(factory));
        var request = new PersonEventRequest { EventType = PersonEventType.Birth, Year = 1991 };

        var result = await sut.CreateAsync(person.Id, request);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task CreateAsync_MarriageType_ReturnsInvalid_NotOnTimeline()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
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

        var sut = new PersonEventService(new EntityFrameworkRepository<PersonEvent>(factory));
        var request = new PersonEventRequest { EventType = PersonEventType.Marriage };

        var result = await sut.CreateAsync(person.Id, request);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetByPersonAsync_ExcludesNonTimelineTypes()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
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
                EventType = PersonEventType.Occupation,
                Year = 2010,
                CreatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var sut = new PersonEventService(new EntityFrameworkRepository<PersonEvent>(factory));

        var result = await sut.GetByPersonAsync(person.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(PersonEventType.Birth, result.Value.First().EventType);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEvent()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
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

        var sut = new PersonEventService(new EntityFrameworkRepository<PersonEvent>(factory));

        var del = await sut.DeleteAsync(evt.Id);

        Assert.True(del.IsSuccess);
        await using var assertDb = InMemoryDb.CreateContext(options);
        Assert.Null(await assertDb.PersonEvents.FindAsync(evt.Id));
    }
}