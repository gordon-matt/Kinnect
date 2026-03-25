using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class DocumentServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesDocumentAndTags()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var uploader = new Person
        {
            FamilyName = "D",
            GivenNames = "U",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(uploader);
        await db.SaveChangesAsync();

        var sut = new DocumentService(
            new EntityFrameworkRepository<Document>(factory),
            new EntityFrameworkRepository<Tag>(factory),
            new EntityFrameworkRepository<DocumentTag>(factory));

        var result = await sut.CreateAsync(
            title: "Will",
            description: null,
            filePath: "docs/will.pdf",
            contentType: "application/pdf",
            fileSize: 1024,
            uploadedByPersonId: uploader.Id,
            tags: ["Legal"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("Will", result.Value.Title);
        Assert.Contains("Legal", result.Value.Tags);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesUploaderName()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var uploader = new Person
        {
            FamilyName = "Smith",
            GivenNames = "Alex",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(uploader);
        await db.SaveChangesAsync();

        db.Documents.Add(new Document
        {
            Title = "Note",
            FilePath = "n.txt",
            ContentType = "text/plain",
            FileSize = 10,
            UploadedByPersonId = uploader.Id,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new DocumentService(
            new EntityFrameworkRepository<Document>(factory),
            new EntityFrameworkRepository<Tag>(factory),
            new EntityFrameworkRepository<DocumentTag>(factory));

        var doc = await db.Documents.FirstAsync();
        var result = await sut.GetByIdAsync(doc.Id);

        Assert.True(result.IsSuccess);
        Assert.Contains("Alex", result.Value.UploadedByName, StringComparison.Ordinal);
        Assert.Contains("Smith", result.Value.UploadedByName, StringComparison.Ordinal);
    }
}