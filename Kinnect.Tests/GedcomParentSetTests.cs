using Kinnect.Models.FamilyTree;
using Kinnect.Services.Abstractions;
using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

/// <summary>Birth, adopted and foster parents going into and out of GEDCOM 5.5.1 and 7.0.</summary>
public class GedcomParentSetTests : IDisposable
{
    private readonly string basePath = Path.Combine(Path.GetTempPath(), "kinnect-gedcom-parents-" + Guid.NewGuid());

    public GedcomParentSetTests() => Directory.CreateDirectory(basePath);

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

    private const string People = """
        0 @I1@ INDI
        1 NAME Adam /Ward/
        1 SEX M
        0 @I2@ INDI
        1 NAME Eve /Ward/
        1 SEX F
        0 @I3@ INDI
        1 NAME Sam /Hart/
        1 SEX M
        0 @I5@ INDI
        1 NAME Fay /Moss/
        1 SEX F
        """;

    private static string File551(string rest) => Wrap("5.5.1", rest);

    private static string Wrap(string version, string rest) =>
        $"0 HEAD\n1 GEDC\n2 VERS {version}\n1 CHAR UTF-8\n{People}\n{rest.Replace("\r\n", "\n")}\n0 TRLR\n".Replace("\r\n", "\n");

    private async Task<(DbContextOptions<ApplicationDbContext> Options, TestDbContextFactory Factory, GedcomImportResult Result)> ImportAsync(string ged, IUserContextService? user = null)
    {
        var (options, factory) = SqliteDb.Create();
        var result = await CreateSut(factory, user).ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(ged)));
        Assert.True(result.IsSuccess, string.Join(" ", result.Errors));
        return (options, factory, result.Value);
    }

    private GedcomService CreateSut(TestDbContextFactory factory, IUserContextService? user = null)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = basePath })
            .Build();

        return new GedcomService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<Photo>(factory),
            new EntityFrameworkRepository<PersonPhoto>(factory),
            new EntityFrameworkRepository<PhotoEvent>(factory),
            new EntityFrameworkRepository<Document>(factory),
            new EntityFrameworkRepository<Video>(factory),
            new EntityFrameworkRepository<Contact>(factory),
            new EntityFrameworkRepository<GedcomFile>(factory),
            new EntityFrameworkRepository<ParentSet>(factory),
            new EntityFrameworkRepository<ParentSetMember>(factory),
            TestParentSets.Create(factory, user),
            new FileStorageService(config, new TestSettingsService()),
            NullLogger<GedcomService>.Instance);
    }

    private async Task<string> ExportAsync(TestDbContextFactory factory, string version, bool includeHidden = false, IUserContextService? user = null)
    {
        var export = GedcomExportOptions.WithoutMedia();
        export.Version = version;
        export.IncludeHiddenParentSets = includeHidden;
        var file = await CreateSut(factory, user).ExportFileAsync(export, progress: null);
        try
        {
            return await File.ReadAllTextAsync(file.Path);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file.Path)!, recursive: true);
        }
    }

    /// <summary>A person's sets as readable text, such as "Adopted*:Sam Hart/-@2001-06-14", so one assert can compare them all.</summary>
    private static async Task<List<string>> DescribeSetsAsync(DbContextOptions<ApplicationDbContext> options, string childGivenName)
    {
        await using var db = SqliteDb.CreateContext(options);
        var child = await db.People.SingleAsync(person => person.GivenNames == childGivenName);
        var sets = await db.ParentSets.Include(set => set.Members).ThenInclude(member => member.ParentPerson)
            .Where(set => set.ChildPersonId == child.Id).OrderBy(set => set.Id).ToListAsync();
        return sets.Select(set =>
        {
            string father = set.Members.FirstOrDefault(member => member.Role == ParentRole.Father)?.ParentPerson.GivenNames ?? "-";
            string mother = set.Members.FirstOrDefault(member => member.Role == ParentRole.Mother)?.ParentPerson.GivenNames ?? "-";
            string date = set.Year is null ? string.Empty : $"@{set.Year:D4}" + (set.Month is null ? string.Empty : $"-{set.Month:D2}") + (set.Day is null ? string.Empty : $"-{set.Day:D2}");
            return $"{set.Type}{(set.IsPrimary ? "*" : string.Empty)}:{father}/{mother}{date}";
        }).ToList();
    }

    // ---- Import ----

    [Fact]
    public async Task Import_FamcWithPedi_MakesABirthAndAnAdoptedSet_AdoptedIsPrimaryByDefault()
    {
        var (options, _, result) = await ImportAsync(File551("""
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 SEX F
            1 FAMC @F1@
            2 PEDI birth
            1 FAMC @F2@
            2 PEDI adopted
            0 @F1@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 CHIL @I4@
            0 @F2@ FAM
            1 HUSB @I3@
            1 CHIL @I4@
            """));

        Assert.Equal(["Birth:Adam/Eve", "Adopted*:Sam/-"], await DescribeSetsAsync(options, "Jane"));
        Assert.Equal(2, result.ParentSetsImported);
    }

    [Fact]
    public async Task Import_ASecondBirthFamily_IsSkipped()
    {
        var (options, _, result) = await ImportAsync(File551("""
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 FAMC @F1@
            2 PEDI birth
            1 FAMC @F2@
            2 PEDI birth
            0 @F1@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 CHIL @I4@
            0 @F2@ FAM
            1 HUSB @I3@
            1 WIFE @I5@
            1 CHIL @I4@
            """));

        Assert.Equal(["Birth*:Adam/Eve"], await DescribeSetsAsync(options, "Jane"));
        Assert.Contains(result.Warnings, warning => warning.Contains("one set of birth parents", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_FamcWithoutPedi_IsABirthSet()
    {
        var (options, _, _) = await ImportAsync(File551("""
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 FAMC @F1@
            0 @F1@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 CHIL @I4@
            """));

        Assert.Equal(["Birth*:Adam/Eve"], await DescribeSetsAsync(options, "Jane"));
    }

    [Fact]
    public async Task Import_AdopEventOnly_MakesAnAdoptedSetWithItsDate_AndNarrowsToTheAdopter()
    {
        var (options, factory, _) = await ImportAsync(File551("""
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 FAMC @F1@
            1 ADOP
            2 DATE 14 JUN 2001
            2 FAMC @F2@
            3 ADOP HUSB
            0 @F1@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 CHIL @I4@
            0 @F2@ FAM
            1 HUSB @I3@
            1 WIFE @I5@
            1 CHIL @I4@
            """));

        Assert.Equal(["Birth:Adam/Eve", "Adopted*:Sam/-@2001-06-14"], await DescribeSetsAsync(options, "Jane"));
        await using var db = SqliteDb.CreateContext(options);
        // The adoption is the set, not a second event on the timeline.
        Assert.DoesNotContain(await db.PersonEvents.ToListAsync(), evt => evt.EventType == PersonEventType.Adoption);
    }

    [Fact]
    public async Task Import_FamcAndAdopForTheSameFamily_ReconcileIntoOneAdoptedSet()
    {
        var (options, _, result) = await ImportAsync(File551("""
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 FAMC @F2@
            2 PEDI adopted
            1 ADOP
            2 DATE 2001
            2 FAMC @F2@
            3 ADOP BOTH
            0 @F2@ FAM
            1 HUSB @I3@
            1 WIFE @I5@
            1 CHIL @I4@
            """));

        Assert.Equal(["Adopted*:Sam/Fay@2001"], await DescribeSetsAsync(options, "Jane"));
        Assert.Equal(1, result.ParentSetsImported);
    }

    [Fact]
    public async Task Import_FosterPedigree_IsAFosterSet()
    {
        var (options, _, _) = await ImportAsync(File551("""
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 FAMC @F2@
            2 PEDI foster
            0 @F2@ FAM
            1 WIFE @I5@
            1 CHIL @I4@
            """));

        Assert.Equal(["Foster*:-/Fay"], await DescribeSetsAsync(options, "Jane"));
    }

    [Fact]
    public async Task Import_SealingAndOtherPedigrees_AreSkippedWithAReportEntry()
    {
        var (options, _, result) = await ImportAsync(Wrap("7.0", """
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 FAMC @F1@
            2 PEDI BIRTH
            1 FAMC @F2@
            2 PEDI SEALING
            1 FAMC @F3@
            2 PEDI OTHER
            2 PHRASE Guardian
            0 @F1@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 CHIL @I4@
            0 @F2@ FAM
            1 HUSB @I3@
            1 CHIL @I4@
            0 @F3@ FAM
            1 WIFE @I5@
            1 CHIL @I4@
            """));

        Assert.Equal(["Birth*:Adam/Eve"], await DescribeSetsAsync(options, "Jane"));
        Assert.Equal(2, result.Warnings.Count(warning => warning.Contains("not supported", StringComparison.Ordinal)));
        Assert.Contains(result.Warnings, warning => warning.Contains("sealing", StringComparison.Ordinal) && warning.Contains("Jane", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_PrimTag_ChoosesThePrimarySet()
    {
        var (options, _, _) = await ImportAsync(File551("""
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 FAMC @F1@
            2 PEDI birth
            2 _PRIM Y
            1 FAMC @F2@
            2 PEDI adopted
            0 @F1@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 CHIL @I4@
            0 @F2@ FAM
            1 HUSB @I3@
            1 CHIL @I4@
            """));

        Assert.Equal(["Birth*:Adam/Eve", "Adopted:Sam/-"], await DescribeSetsAsync(options, "Jane"));
    }

    [Fact]
    public async Task Import_ASetThatBreaksTheRules_IsSkippedAndExplained()
    {
        var (options, _, result) = await ImportAsync(File551("""
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 BIRT
            2 DATE 1990
            1 FAMC @F2@
            2 PEDI adopted
            0 @F2@ FAM
            1 HUSB @I6@
            1 CHIL @I4@
            0 @I6@ INDI
            1 NAME Late /Born/
            1 SEX M
            1 BIRT
            2 DATE 2000
            """));

        Assert.Empty(await DescribeSetsAsync(options, "Jane"));
        Assert.Contains(result.Warnings, warning => warning.Contains("Jane", StringComparison.Ordinal) && warning.Contains("born before", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_TheSameFileTwice_DoesNotDuplicateSets()
    {
        string ged = File551("""
            0 @I4@ INDI
            1 NAME Jane /Ward/
            1 FAMC @F1@
            2 PEDI birth
            1 FAMC @F2@
            2 PEDI adopted
            0 @F1@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 CHIL @I4@
            0 @F2@ FAM
            1 HUSB @I3@
            1 CHIL @I4@
            """);
        var (options, factory, _) = await ImportAsync(ged);

        var again = await CreateSut(factory).ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(ged)));

        Assert.True(again.IsSuccess);
        Assert.Equal(0, again.Value.ParentSetsImported);
        Assert.Equal(2, (await DescribeSetsAsync(options, "Jane")).Count);
    }

    // ---- Export ----

    private static async Task<TestDbContextFactory> SeedForExportAsync(Action<ApplicationDbContext, Dictionary<string, Person>, List<Func<Task>>>? extra = null)
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var people = new Dictionary<string, Person>();
        foreach ((string name, bool male) in new[] { ("Adam", true), ("Eve", false), ("Sam", true), ("Fay", false), ("Jane", false) })
        {
            var person = new Person { FamilyName = "Ward", GivenNames = name, IsMale = male, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
            db.People.Add(person);
            people[name] = person;
        }

        await db.SaveChangesAsync();
        var jane = people["Jane"];
        var birth = await TestParentSets.LinkAsync(db, jane, people["Adam"], people["Eve"], ParentageType.Birth, isPrimary: false);
        var adopted = await TestParentSets.LinkAsync(db, jane, people["Sam"], null, ParentageType.Adopted, isPrimary: true);
        adopted.Year = 2001;
        adopted.Month = 6;
        adopted.Day = 14;
        await db.SaveChangesAsync();
        await TestParentSets.LinkAsync(db, jane, null, people["Fay"], ParentageType.Foster, isPrimary: false);
        return factory;
    }

    [Fact]
    public async Task Export_551_WritesAFamilyPerSet_WithLowercasePedi_PrimaryFirst()
    {
        var factory = await SeedForExportAsync();

        string ged = await ExportAsync(factory, "5.5.1");

        Assert.Contains("2 PEDI adopted", ged, StringComparison.Ordinal);
        Assert.Contains("2 PEDI birth", ged, StringComparison.Ordinal);
        Assert.Contains("2 PEDI foster", ged, StringComparison.Ordinal);
        Assert.DoesNotContain("PEDI ADOPTED", ged, StringComparison.Ordinal);
        Assert.Contains("2 _PRIM Y", ged, StringComparison.Ordinal);
        Assert.Equal(1, Count(ged, "2 _PRIM Y"));
        Assert.Equal(3, Count(ged, "0 @F"));

        // The primary FAMC (the adoption) is first in Jane's record.
        string jane = IndividualRecord(ged, "Jane");
        Assert.True(jane.IndexOf("PEDI adopted", StringComparison.Ordinal) < jane.IndexOf("PEDI birth", StringComparison.Ordinal));
        Assert.True(jane.IndexOf("_PRIM Y", StringComparison.Ordinal) < jane.IndexOf("PEDI birth", StringComparison.Ordinal));

        // The adoption event carries the date and who adopted.
        Assert.Contains("1 ADOP", jane, StringComparison.Ordinal);
        Assert.Contains("2 DATE 14 JUN 2001", jane, StringComparison.Ordinal);
        Assert.Contains("3 ADOP HUSB", jane, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_70_WritesUppercasePedi()
    {
        var factory = await SeedForExportAsync();

        string ged = await ExportAsync(factory, "7.0");

        Assert.Contains("2 PEDI ADOPTED", ged, StringComparison.Ordinal);
        Assert.Contains("2 PEDI BIRTH", ged, StringComparison.Ordinal);
        Assert.Contains("2 PEDI FOSTER", ged, StringComparison.Ordinal);
        Assert.Contains("_PRIM Y", ged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_SingleParentSet_IsAFamilyWithOneParent()
    {
        var factory = await SeedForExportAsync();

        string ged = await ExportAsync(factory, "5.5.1");

        string[] families = ged.Split("\n0 ").Where(block => block.StartsWith("@F", StringComparison.Ordinal)).ToArray();
        Assert.Contains(families, family => family.Contains("HUSB", StringComparison.Ordinal) && !family.Contains("WIFE", StringComparison.Ordinal));
        Assert.Contains(families, family => family.Contains("WIFE", StringComparison.Ordinal) && !family.Contains("HUSB", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_OmitsHiddenSetsByDefault_AndIncludesThemOnlyOnRequestForTheAllowed()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var jane = new Person { FamilyName = "Ward", GivenNames = "Jane", IsMale = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, UserId = "jane" };
        var adam = new Person { FamilyName = "Ward", GivenNames = "Adam", IsMale = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var sam = new Person { FamilyName = "Hart", GivenNames = "Sam", IsMale = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var gus = new Person { FamilyName = "Hart", GivenNames = "Gus", IsMale = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(jane, adam, sam, gus);
        await db.SaveChangesAsync();
        await TestParentSets.LinkAsync(db, jane, adam, isPrimary: true);
        await TestParentSets.LinkAsync(db, jane, sam, type: ParentageType.Adopted, isPrimary: false, visibility: LinkVisibility.FamilyAdmins);
        await TestParentSets.LinkAsync(db, jane, gus, type: ParentageType.Foster, isPrimary: false, visibility: LinkVisibility.ChildOnly);

        string byDefault = await ExportAsync(factory, "5.5.1", includeHidden: false);
        string forAdmin = await ExportAsync(factory, "5.5.1", includeHidden: true, user: TestUserContext.Admin);
        string forChild = await ExportAsync(factory, "5.5.1", includeHidden: true, user: new TestUserContext("jane"));
        string forMember = await ExportAsync(factory, "5.5.1", includeHidden: true, user: new TestUserContext("member", isEditor: true));

        Assert.Equal(1, Count(byDefault, "PEDI"));
        Assert.DoesNotContain("PEDI adopted", byDefault, StringComparison.Ordinal);
        Assert.Contains("PEDI adopted", forAdmin, StringComparison.Ordinal);
        Assert.DoesNotContain("PEDI foster", forAdmin, StringComparison.Ordinal);
        Assert.Contains("PEDI foster", forChild, StringComparison.Ordinal);
        Assert.DoesNotContain("PEDI adopted", forMember, StringComparison.Ordinal);
        Assert.Equal(1, Count(forMember, "PEDI"));
    }

    [Theory]
    [InlineData("5.5.1")]
    [InlineData("7.0")]
    public async Task ExportThenImport_KeepsTypesMembersPrimaryAndAdoptionDate(string version)
    {
        var factory = await SeedForExportAsync();
        string ged = await ExportAsync(factory, version);

        var (options, _, result) = await ImportAsync(ged);

        Assert.Equal(
            // The primary set is written first, so it is also the first one read back.
            ["Adopted*:Sam/-@2001-06-14", "Birth:Adam/Eve", "Foster:-/Fay"],
            await DescribeSetsAsync(options, "Jane"));
        Assert.Equal(3, result.ParentSetsImported);
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string IndividualRecord(string ged, string givenName)
    {
        string normalized = ged.Replace("\r\n", "\n");
        return normalized.Split("\n0 ").First(block => block.Contains("INDI", StringComparison.Ordinal) && block.Contains(givenName, StringComparison.Ordinal));
    }
}
