using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class ParentSetBackupTests : IDisposable
{
    private readonly string basePath = Path.Combine(Path.GetTempPath(), "kinnect-backup-tests-" + Guid.NewGuid());

    public ParentSetBackupTests() => Directory.CreateDirectory(basePath);

    public void Dispose()
    {
        try
        {
            Directory.Delete(basePath, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup of temp files
        }
    }

    private PersonBackupService CreateSut(ApplicationDbContext db)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = basePath })
            .Build();
        var settings = new TestSettingsService();
        return new PersonBackupService(db, new FileStorageService(config, settings), settings, NullLogger<PersonBackupService>.Instance);
    }

    private static Person Make(string name, bool male) =>
        new() { FamilyName = "Ward", GivenNames = name, IsMale = male, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };

    [Fact]
    public async Task RoundTrip_KeepsEverySet_IncludingHiddenOnes_AndTheirOrderAndVisibility()
    {
        var (options, _) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        Person jane = Make("Jane", false), adam = Make("Adam", true), eve = Make("Eve", false), sam = Make("Sam", true);
        db.People.AddRange(jane, adam, eve, sam);
        await db.SaveChangesAsync();
        await TestParentSets.LinkAsync(db, jane, adam, eve, isPrimary: false);
        var adopted = await TestParentSets.LinkAsync(db, jane, sam, null, ParentageType.Adopted, isPrimary: true, visibility: LinkVisibility.FamilyAdmins);
        adopted.Year = 2001;
        adopted.Month = 6;
        adopted.Day = 14;
        await db.SaveChangesAsync();
        var sut = CreateSut(db);
        await sut.ExportToNewFileAsync();
        var file = (await sut.ListBackupsAsync()).Value.Single();

        await db.ParentSets.ExecuteDeleteAsync();
        Assert.Equal(0, await db.ParentSetMembers.CountAsync());
        var restored = await sut.RestoreFromFileAsync(file.FileName);

        Assert.True(restored.IsSuccess, string.Join(" ", restored.Errors));
        var sets = await db.ParentSets.Include(set => set.Members).OrderBy(set => set.Id).ToListAsync();
        Assert.Equal(2, sets.Count);
        Assert.Equal([ParentageType.Birth, ParentageType.Adopted], sets.Select(set => set.Type));
        Assert.Equal([false, true], sets.Select(set => set.IsPrimary));
        Assert.Equal([LinkVisibility.Everyone, LinkVisibility.FamilyAdmins], sets.Select(set => set.Visibility));
        Assert.Equal((short?)2001, sets[1].Year);
        Assert.Equal((byte?)6, sets[1].Month);
        Assert.Equal((byte?)14, sets[1].Day);
        Assert.Equal([adam.Id, eve.Id], sets[0].Members.OrderBy(member => member.Role).Select(member => member.ParentPersonId));
        Assert.Equal([ParentRole.Father, ParentRole.Mother], sets[0].Members.OrderBy(member => member.Role).Select(member => member.Role));
    }

    [Fact]
    public async Task Restore_AVersion5File_TurnsFatherAndMotherIntoOnePrimaryBirthSet()
    {
        var (options, _) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        Person jane = Make("Jane", false), adam = Make("Adam", true), eve = Make("Eve", false);
        db.People.AddRange(jane, adam, eve);
        await db.SaveChangesAsync();
        string directory = Path.Combine(basePath, Constants.FileStorage.PersonTreeBackups);
        Directory.CreateDirectory(directory);
        string json = $$"""
            {
              "schemaVersion": 5,
              "exportedAtUtc": "2026-01-01T00:00:00Z",
              "people": [
                { "id": {{jane.Id}}, "givenNames": "Jane", "familyName": "Ward", "fatherId": {{adam.Id}}, "motherId": {{eve.Id}}, "createdAtUtc": "2026-01-01T00:00:00Z", "updatedAtUtc": "2026-01-01T00:00:00Z" },
                { "id": {{adam.Id}}, "givenNames": "Adam", "familyName": "Ward", "createdAtUtc": "2026-01-01T00:00:00Z", "updatedAtUtc": "2026-01-01T00:00:00Z" },
                { "id": {{eve.Id}}, "givenNames": "Eve", "familyName": "Ward", "createdAtUtc": "2026-01-01T00:00:00Z", "updatedAtUtc": "2026-01-01T00:00:00Z" }
              ]
            }
            """;
        string name = "person-backup-20260101-000000.json";
        await File.WriteAllTextAsync(Path.Combine(directory, name), json);

        var restored = await CreateSut(db).RestoreFromFileAsync(name);

        Assert.True(restored.IsSuccess, string.Join(" ", restored.Errors));
        var set = await db.ParentSets.Include(candidate => candidate.Members).SingleAsync();
        Assert.Equal(jane.Id, set.ChildPersonId);
        Assert.Equal(ParentageType.Birth, set.Type);
        Assert.True(set.IsPrimary);
        Assert.Equal(LinkVisibility.Everyone, set.Visibility);
        Assert.Equal(adam.Id, set.Members.Single(member => member.Role == ParentRole.Father).ParentPersonId);
        Assert.Equal(eve.Id, set.Members.Single(member => member.Role == ParentRole.Mother).ParentPersonId);
    }
}
