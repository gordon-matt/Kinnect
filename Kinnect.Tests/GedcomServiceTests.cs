using System.IO.Compression;
using System.Security.Cryptography;
using Kinnect.Models.FamilyTree;
using Kinnect.Tests.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Kinnect.Tests;

public class GedcomServiceTests : IDisposable
{
    private readonly string basePath = Path.Combine(Path.GetTempPath(), "kinnect-gedcom-tests-" + Guid.NewGuid());

    public GedcomServiceTests() => Directory.CreateDirectory(basePath);

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

    [Fact]
    public async Task ExportAsync_ContainsIndividualAndBirth()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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

        var sut = CreateSut(factory);

        string gedcom = await sut.ExportAsync();

        Assert.Contains("0 HEAD", gedcom, StringComparison.Ordinal);
        Assert.Contains("INDI", gedcom, StringComparison.Ordinal);
        Assert.Contains("John", gedcom, StringComparison.Ordinal);
        Assert.Contains("Doe", gedcom, StringComparison.Ordinal);
        Assert.Contains("BIRT", gedcom, StringComparison.Ordinal);
        Assert.Contains("London", gedcom, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportFileAsync_GedcomXJson_WritesPeopleAndBirth()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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

        var export = GedcomExportOptions.WithoutMedia();
        export.Version = "gedcomx-json";
        var file = await CreateSut(factory).ExportFileAsync(export, progress: null);

        try
        {
            string json = await File.ReadAllTextAsync(file.Path);
            Assert.Equal("kinnect.json", file.DownloadName);
            Assert.Contains("\"persons\"", json, StringComparison.Ordinal);
            Assert.Contains("John", json, StringComparison.Ordinal);
            Assert.Contains("Doe", json, StringComparison.Ordinal);
            Assert.Contains("http://gedcomx.org/Birth", json, StringComparison.Ordinal);
            Assert.Contains("London", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file.Path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_PlaceMap_StoresCoordinates()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);

        string ged = """
            0 HEAD
            1 GEDC
            2 VERS 5.5.1
            0 @I1@ INDI
            1 NAME Jane /Roe/
            1 BIRT
            2 PLAC Boston, Massachusetts, USA
            3 MAP
            4 LATI N42.3601
            4 LONG W71.0589
            0 TRLR
            """;

        var result = await sut.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(ged)));

        Assert.True(result.IsSuccess);
        await using var db = SqliteDb.CreateContext(options);
        var birth = await db.PersonEvents.SingleAsync();
        Assert.Equal(42.3601, birth.Latitude!.Value, precision: 4);
        Assert.Equal(-71.0589, birth.Longitude!.Value, precision: 4);
    }

    [Fact]
    public async Task ImportAsync_MinimalFile_ImportsIndividual()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);

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
        await using var db = SqliteDb.CreateContext(options);
        Assert.NotEmpty(await db.People.ToListAsync());
    }

    [Fact]
    public async Task ImportAsync_StoresTheOriginalFileUnchanged()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);
        byte[] bytes = Encoding.UTF8.GetBytes(SimpleGedcom("photos/a.png", withEventMedia: false));

        await using var stream = new MemoryStream(bytes);
        var result = await sut.ImportAsync(stream, "family.ged");

        Assert.True(result.IsSuccess);
        await using var db = SqliteDb.CreateContext(options);
        var stored = Assert.Single(await db.GedcomFiles.ToListAsync());
        Assert.Equal("family.ged", stored.FileName);
        Assert.Equal(bytes.Length, stored.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), stored.Sha256);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(basePath, stored.StoragePath)));
    }

    [Fact]
    public async Task ImportAsync_PlainGedWithMediaReferences_ImportsNoPhotosAndExplainsWhy()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleGedcom("photos/a.png", withEventMedia: true)));
        var result = await sut.ImportAsync(stream, "family.ged");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.PhotosImported);
        Assert.Equal(2, result.Value.MediaMissing);
        Assert.Contains(result.Value.Warnings, w => w.Contains("upload a .zip", StringComparison.Ordinal));
        await using var db = SqliteDb.CreateContext(options);
        Assert.Empty(await db.Photos.ToListAsync());
    }

    [Fact]
    public async Task ImportAsync_ZipWithMedia_ImportsPhotosLinksThemAndSetsProfilePicture()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);

        // The same image is used for the person and for her birth, so it must be stored once.
        using var zip = CreateZip(
            ("export/family.ged", Encoding.UTF8.GetBytes(SimpleGedcom("photos/a.png", withEventMedia: true))),
            ("export/photos/a.png", CreatePng()));

        var result = await sut.ImportAsync(zip, "family.zip");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Equal(1, result.Value.PhotosImported);
        Assert.Equal(0, result.Value.MediaMissing);

        await using var db = SqliteDb.CreateContext(options);
        var person = await db.People.SingleAsync();
        var photo = await db.Photos.SingleAsync();
        Assert.Equal("Jane at home", photo.Title);
        Assert.Equal("photos/a.png", photo.GedcomFileReference);
        Assert.Equal(person.Id, photo.UploadedByPersonId);
        Assert.True(File.Exists(Path.Combine(basePath, photo.FilePath)));
        Assert.NotNull(photo.ThumbnailPath);

        var link = await db.PersonPhotos.SingleAsync();
        Assert.Equal((person.Id, photo.Id), (link.PersonId, link.PhotoId));

        var eventLink = await db.PhotoEvents.SingleAsync();
        var birth = await db.PersonEvents.SingleAsync(e => e.EventType == PersonEventType.Birth);
        Assert.Equal(birth.Id, eventLink.PersonEventId);

        Assert.NotNull(person.ProfileImagePath);
        Assert.True(File.Exists(Path.Combine(basePath, person.ProfileImagePath)));

        // The stored original is the .ged itself, not the zip.
        var stored = await db.GedcomFiles.SingleAsync();
        Assert.Equal("family.ged", stored.FileName);
    }

    [Fact]
    public async Task ImportAsync_GedcomXZip_ImportsPhotosFromThePackage()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);

        string json = """
            {
              "persons": [
                {
                  "id": "I1",
                  "gender": { "type": "http://gedcomx.org/Female" },
                  "names": [
                    {
                      "nameForms": [
                        {
                          "fullText": "Jane Roe",
                          "parts": [
                            { "type": "http://gedcomx.org/Given", "value": "Jane" },
                            { "type": "http://gedcomx.org/Surname", "value": "Roe" }
                          ]
                        }
                      ]
                    }
                  ],
                  "media": [ { "description": "#M1" } ]
                }
              ],
              "sourceDescriptions": [
                {
                  "id": "M1",
                  "resourceType": "http://gedcomx.org/DigitalArtifact",
                  "mediaType": "image/png",
                  "about": "media/a.png",
                  "titles": [ { "value": "Jane at home" } ]
                }
              ]
            }
            """;
        using var zip = CreateZip(
            ("tree.json", Encoding.UTF8.GetBytes(json)),
            ("media/a.png", CreatePng()));

        var result = await sut.ImportAsync(zip, "kinnect.zip");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Equal(1, result.Value.PhotosImported);
        Assert.Equal(0, result.Value.MediaMissing);
        await using var db = SqliteDb.CreateContext(options);
        var person = await db.People.SingleAsync();
        var photo = await db.Photos.SingleAsync();
        Assert.Equal("Jane at home", photo.Title);
        Assert.NotNull(person.ProfileImagePath);
    }

    [Fact]
    public async Task ImportAsync_ZipMissingAFile_ReportsItAndStillImportsThePerson()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);

        using var zip = CreateZip(("family.ged", Encoding.UTF8.GetBytes(SimpleGedcom("photos/gone.png", withEventMedia: false))));

        var result = await sut.ImportAsync(zip);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.PeopleImported);
        Assert.Equal(1, result.Value.MediaMissing);
        Assert.Contains(result.Value.Warnings, w => w.Contains("gone.png", StringComparison.Ordinal));
        await using var db = SqliteDb.CreateContext(options);
        Assert.Empty(await db.Photos.ToListAsync());
    }

    [Fact]
    public async Task ImportAsync_NonImageAndWebLinks_AreSkipped()
    {
        var (_, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);

        string ged = """
            0 HEAD
            1 GEDC
            2 VERS 5.5.1
            0 @I1@ INDI
            1 NAME Jane /Roe/
            1 OBJE
            2 FORM pdf
            2 FILE docs/certificate.pdf
            1 OBJE
            2 FORM URL
            2 FILE http://example.com/jane
            0 TRLR
            """;
        using var zip = CreateZip(
            ("family.ged", Encoding.UTF8.GetBytes(ged)),
            ("docs/certificate.pdf", "%PDF-1.4"u8.ToArray()));

        var result = await sut.ImportAsync(zip);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.PhotosImported);
        Assert.Equal(1, result.Value.DocumentsImported);
        Assert.Equal(1, result.Value.MediaSkipped);
    }

    [Fact]
    public async Task ImportAsync_ImportsUnknownSexNotesContactsAndCustomEvents()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);

        string ged = """
            0 HEAD
            1 GEDC
            2 VERS 5.5.1
            0 @I1@ INDI
            1 NAME John Fitzgerald /KENNEDY/
            2 NPFX Dr
            2 GIVN John, Fitzgerald
            2 NICK Jack
            2 SURN KENNEDY
            2 NSFX Jr
            1 SEX M
            0 @I2@ INDI
            1 NAME Alex /Kay/
            1 SEX U
            1 NOTE Kept the family papers.
            1 EMIG
            2 DATE 1901
            2 PLAC Liverpool
            1 EVEN
            2 TYPE Enlisted
            2 DATE 1914
            1 _MILT
            2 DATE 1917
            2 PLAC France
            1 OCCU Clerk
            1 ADDR 12 King Street
            2 CITY Adelaide
            1 EMAIL alex@example.com
            1 PHON 555-0100
            0 TRLR
            """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ged));
        var result = await sut.ImportAsync(stream, "people.ged");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        await using var db = SqliteDb.CreateContext(options);
        var people = await db.People.ToListAsync();
        var kennedy = people.Single(person => person.FamilyName == "KENNEDY");
        Assert.Equal("John Fitzgerald", kennedy.GivenNames);
        Assert.Equal("Dr", kennedy.NamePrefix);
        Assert.Equal("Jack", kennedy.Nickname);
        Assert.Equal("Jr", kennedy.NameSuffix);

        var person = people.Single(person => person.GivenNames == "Alex");
        Assert.Null(person.IsMale);
        Assert.Equal("Kept the family papers.", person.Note);
        Assert.Equal("Clerk", person.Occupation);

        var contacts = await db.Contacts.Where(contact => contact.PersonId == person.Id).OrderBy(c => c.SortOrder).ToListAsync();
        Assert.Equal([ContactType.Email, ContactType.Phone, ContactType.Address], contacts.Select(c => c.Type));
        Assert.Equal("alex@example.com", contacts[0].Value);
        Assert.Contains("12 King Street", contacts[2].Value);
        Assert.Contains("Adelaide", contacts[2].Value);

        var events = await db.PersonEvents.ToListAsync();
        Assert.Contains(events, e => e.EventType == PersonEventType.Emigration && e.Place == "Liverpool");
        Assert.Contains(events, e => e.EventType == PersonEventType.Custom && e.Description == "Enlisted");
        Assert.Contains(events, e => e.EventType == "_MILT" && e.Place == "France" && e.Description == null);

        string exported = await sut.ExportAsync();
        Assert.Contains("2 NPFX Dr", exported, StringComparison.Ordinal);
        Assert.Contains("2 NICK Jack", exported, StringComparison.Ordinal);
        Assert.Contains("2 NSFX Jr", exported, StringComparison.Ordinal);
        Assert.Contains("1 _MILT", exported, StringComparison.Ordinal);
        Assert.DoesNotContain("2 TYPE _MILT", exported, StringComparison.Ordinal);
        Assert.Contains(events, e => e.EventType == PersonEventType.Occupation && e.Description == "Clerk");
    }

    [Fact]
    public async Task ExportAsync_KeepsTagsThatWereNotImportedAndAppliesEdits()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);
        string ged = """
            0 HEAD
            1 GEDC
            2 VERS 5.5.1
            1 CHAR UTF-8
            0 @I1@ INDI
            1 NAME John /Smith/
            2 NICK Jack
            1 NAME John /Other/
            2 TYPE also known as
            1 SEX M
            1 REFN 12345
            1 AFN ABCD-EF
            1 CHAN
            2 DATE 1 JAN 2000
            1 ALIA @I9@
            1 BIRT
            2 DATE 1 JAN 1900
            2 PLAC Boston
            2 CAUS Fever
            2 SOUR @S1@
            1 NOTE @N1@
            0 @N1@ NOTE Kept aside.
            1 SOUR @S1@
            0 @S1@ SOUR
            1 TITL The parish book
            0 TRLR
            """;

        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(ged)))
        {
            var imported = await sut.ImportAsync(stream, "keep.ged");
            Assert.True(imported.IsSuccess, string.Join("; ", imported.Errors));
        }

        await using (var db = SqliteDb.CreateContext(options))
        {
            var person = await db.People.SingleAsync();
            person.Nickname = "Johnny";
            var birth = await db.PersonEvents.SingleAsync(evt => evt.EventType == PersonEventType.Birth);
            birth.Year = 1901;
            await db.SaveChangesAsync();
        }

        string exported = await sut.ExportAsync();
        Assert.Contains("2 NICK Johnny", exported, StringComparison.Ordinal);
        Assert.Contains("1 NAME John /Other/", exported, StringComparison.Ordinal);
        Assert.Contains("2 TYPE also known as", exported, StringComparison.Ordinal);
        Assert.Contains("1 REFN 12345", exported, StringComparison.Ordinal);
        Assert.Contains("1 AFN ABCD-EF", exported, StringComparison.Ordinal);
        Assert.Contains("1 CHAN", exported, StringComparison.Ordinal);
        Assert.Contains("1 ALIA @I9@", exported, StringComparison.Ordinal);
        Assert.Contains("2 CAUS Fever", exported, StringComparison.Ordinal);
        Assert.Contains("2 SOUR @S1@", exported, StringComparison.Ordinal);
        Assert.Contains("1 TITL The parish book", exported, StringComparison.Ordinal);
        Assert.Contains("1 NOTE @N1@", exported, StringComparison.Ordinal);
        Assert.Contains("0 @N1@ NOTE Kept aside.", exported, StringComparison.Ordinal);
        Assert.Contains("01 JAN 1901", exported, StringComparison.Ordinal);
        Assert.DoesNotContain("01 JAN 1900", exported, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_MediaDateAndText_UsesTakenOnForAnExactDateAndDescriptionForAPhrase()
    {
        var (options, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);
        string ged = """
            0 HEAD
            1 GEDC
            2 VERS 5.5.1
            0 @I1@ INDI
            1 NAME Jane /Roe/
            1 OBJE
            2 FILE photos/a.png
            3 FORM png
            3 _DATE 15 MAR 1920
            3 _TEXT At the house
            1 OBJE
            2 FILE docs/note.txt
            3 FORM txt
            3 _DATE ABT 1900
            3 _TEXT A rough date
            0 TRLR
            """;
        using var zip = CreateZip(
            ("family.ged", Encoding.UTF8.GetBytes(ged)),
            ("photos/a.png", CreatePng()),
            ("docs/note.txt", "hello"u8.ToArray()));

        var result = await sut.ImportAsync(zip, "family.zip");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        await using var db = SqliteDb.CreateContext(options);
        var photo = await db.Photos.SingleAsync();
        Assert.Equal((short)1920, photo.YearTaken);
        Assert.Equal((byte)3, photo.MonthTaken);
        Assert.Equal((byte)15, photo.DayTaken);
        Assert.Equal("At the house", photo.Description);

        var document = await db.Documents.SingleAsync();
        Assert.Equal("Date: ABT 1900\n\nA rough date", document.Description);
    }

    [Fact]
    public async Task ImportAsync_ZipWithoutGedcom_Fails()
    {
        var (_, factory) = SqliteDb.Create();
        var sut = CreateSut(factory);

        using var zip = CreateZip(("photos/a.png", CreatePng()));

        var result = await sut.ImportAsync(zip);

        Assert.False(result.IsSuccess);
    }

    private GedcomService CreateSut(TestDbContextFactory factory)
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
            new FileStorageService(config, new TestSettingsService()),
            NullLogger<GedcomService>.Instance);
    }

    private static string SimpleGedcom(string photoPath, bool withEventMedia)
    {
        string eventMedia = withEventMedia
            ? $"""
              2 OBJE
              3 FORM png
              3 FILE {photoPath}
              """
            : string.Empty;

        return $"""
            0 HEAD
            1 GEDC
            2 VERS 5.5.1
            0 @I1@ INDI
            1 NAME Jane /Roe/
            1 SEX F
            1 BIRT
            2 DATE 1 JAN 1950
            {eventMedia}
            1 OBJE
            2 FORM png
            2 TITL Jane at home
            2 FILE {photoPath}
            0 TRLR
            """;
    }

    private static MemoryStream CreateZip(params (string Name, byte[] Content)[] files)
    {
        var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(content);
            }
        }

        buffer.Position = 0;
        return buffer;
    }

    private static byte[] CreatePng()
    {
        using var image = new Image<Rgba32>(40, 30);
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }
}

public class GedcomMediaLocatorTests
{
    [Theory]
    [InlineData("photos/a.jpg", "photos/a.jpg")]
    [InlineData("photos\\a.jpg", "photos/a.jpg")]
    [InlineData("./photos/a.jpg", "photos/a.jpg")]
    [InlineData("PHOTOS/A.JPG", "photos/a.jpg")]
    [InlineData("C:\\Users\\jo\\Pictures\\photos\\a.jpg", "photos/a.jpg")]
    [InlineData("C:\\Users\\jo\\Pictures\\a.jpg", "photos/a.jpg")]
    [InlineData("file:///C:/Users/jo/photos/a.jpg", "photos/a.jpg")]
    [InlineData("../photos/a.jpg", "photos/a.jpg")]
    public void Resolve_FindsTheFileHoweverTheExporterWroteThePath(string reference, string expected)
    {
        var sut = new GedcomMediaLocator(["photos/a.jpg", "photos/b.jpg"], "tree.ged");

        var result = sut.Resolve(reference);

        Assert.Equal(GedcomMediaLookupStatus.Found, result.Status);
        Assert.Equal(expected, result.EntryName);
    }

    [Fact]
    public void Resolve_ResolvesRelativeToTheGedcomFolder()
    {
        var sut = new GedcomMediaLocator(["export/media/a.jpg", "other/a.jpg"], "export/tree.ged");

        Assert.Equal("export/media/a.jpg", sut.Resolve("media/a.jpg").EntryName);
    }

    [Fact]
    public void Resolve_PrefersTheLongestMatchingTail()
    {
        var sut = new GedcomMediaLocator(["x/a.jpg", "kennedy/a.jpg"], "tree.ged");

        Assert.Equal("kennedy/a.jpg", sut.Resolve("C:\\Pics\\kennedy\\a.jpg").EntryName);
    }

    [Fact]
    public void Resolve_PrefersTheCopyNearestTheGedcom_WhenNamesAreAmbiguous()
    {
        var sut = new GedcomMediaLocator(["old/a.jpg", "export/deep/er/a.jpg", "export/a.jpg"], "export/tree.ged");

        Assert.Equal("export/a.jpg", sut.Resolve("D:\\somewhere\\a.jpg").EntryName);
    }

    [Fact]
    public void Resolve_MatchesAcrossUnicodeNormalisationForms()
    {
        // macOS zips store "ë" decomposed; GEDCOMs usually hold it composed.
        string decomposed = "photos/Nomino\u0065\u0308.jpg";
        string composed = "photos/Nomino\u00EB.jpg";
        var sut = new GedcomMediaLocator([decomposed], "tree.ged");

        Assert.Equal(GedcomMediaLookupStatus.Found, sut.Resolve(composed).Status);
    }

    [Fact]
    public void Resolve_DecodesPercentEncodedFileUris()
    {
        var sut = new GedcomMediaLocator(["photos/my photo.jpg"], "tree.ged");

        Assert.Equal("photos/my photo.jpg", sut.Resolve("file:///C:/x/photos/my%20photo.jpg").EntryName);
    }

    [Theory]
    [InlineData("http://example.com/a.jpg", null)]
    [InlineData("https://example.com/a.jpg", null)]
    [InlineData("mailto:a@b.com", null)]
    [InlineData("homer@springfield.com", "URL")]
    public void Resolve_NeverTreatsWebAddressesAsFiles(string reference, string? format)
    {
        var sut = new GedcomMediaLocator(["a.jpg"], "tree.ged");

        Assert.Equal(GedcomMediaLookupStatus.Remote, sut.Resolve(reference, format).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("c:\\users\\merri\\documents\\Photos\\")]
    [InlineData("photos/")]
    public void Resolve_ReportsFolderOnlyAndEmptyReferences(string? reference)
    {
        var sut = new GedcomMediaLocator(["photos/a.jpg"], "tree.ged");

        Assert.Equal(GedcomMediaLookupStatus.NoFileName, sut.Resolve(reference).Status);
    }

    [Fact]
    public void Resolve_ReportsMissingFiles()
    {
        var sut = new GedcomMediaLocator(["photos/a.jpg"], "tree.ged");

        Assert.Equal(GedcomMediaLookupStatus.Missing, sut.Resolve("photos/zzz.jpg").Status);
    }

    [Fact]
    public void Constructor_IgnoresDirectoryEntries()
    {
        var sut = new GedcomMediaLocator(["photos/", "photos/a.jpg"], "tree.ged");

        Assert.Equal(1, sut.FileCount);
    }
}
