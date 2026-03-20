using Ardalis.Result;
using Kinnect.Models;
using Kinnect.Services.Abstractions;

namespace Kinnect.Services;

public class DocumentService(IRepository<Document> documentRepository, IRepository<Tag> tagRepository, IRepository<DocumentTag> documentTagRepository) : IDocumentService
{
    public async Task<Result<IEnumerable<DocumentDto>>> GetByPersonAsync(int personId)
    {
        var documents = await documentRepository.FindAsync(new SearchOptions<Document>
        {
            Query = x => x.UploadedByPersonId == personId,
            Include = q => q.Include(d => d.UploadedBy).Include(d => d.DocumentTags).ThenInclude(dt => dt.Tag)
        });

        return Result.Success(documents.OrderByDescending(d => d.CreatedAtUtc).Select(MapToDto));
    }

    public async Task<Result<DocumentDto>> GetByIdAsync(int id)
    {
        var documents = await documentRepository.FindAsync(new SearchOptions<Document>
        {
            Query = x => x.Id == id,
            Include = q => q.Include(d => d.UploadedBy).Include(d => d.DocumentTags).ThenInclude(dt => dt.Tag)
        });
        var document = documents.FirstOrDefault();

        if (document is null)
            return Result.NotFound("Document not found.");

        return Result.Success(MapToDto(document));
    }

    public async Task<Result<DocumentDto>> CreateAsync(string title, string? description, string filePath, string contentType, long fileSize, int uploadedByPersonId, List<string>? tags)
    {
        var document = new Document
        {
            Title = title,
            Description = description,
            FilePath = filePath,
            ContentType = contentType,
            FileSize = fileSize,
            UploadedByPersonId = uploadedByPersonId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await documentRepository.InsertAsync(document);

        if (tags is { Count: > 0 })
        {
            await SyncTagsAsync(document.Id, tags);
        }

        return Result.Success(new DocumentDto
        {
            Id = document.Id,
            Title = document.Title,
            FilePath = document.FilePath,
            Description = document.Description,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            UploadedByPersonId = document.UploadedByPersonId,
            CreatedAtUtc = document.CreatedAtUtc,
            Tags = tags ?? []
        });
    }

    public async Task<Result> UpdateTagsAsync(int id, List<string> tags)
    {
        var document = await documentRepository.FindOneAsync(id);
        if (document is null)
            return Result.NotFound("Document not found.");

        await SyncTagsAsync(id, tags);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId)
    {
        var document = await documentRepository.FindOneAsync(id);
        if (document is null)
            return Result.NotFound("Document not found.");

        await documentRepository.DeleteAsync(document);
        return Result.Success();
    }

    private async Task SyncTagsAsync(int documentId, List<string> tagNames)
    {
        var existing = await documentTagRepository.FindAsync(new SearchOptions<DocumentTag>
        {
            Query = x => x.DocumentId == documentId
        });

        foreach (var dt in existing)
        {
            await documentTagRepository.DeleteAsync(dt);
        }

        foreach (string tagName in tagNames.Distinct())
        {
            var tags = await tagRepository.FindAsync(new SearchOptions<Tag>
            {
                Query = x => x.Name == tagName
            });
            var tag = tags.FirstOrDefault();

            if (tag is null)
            {
                tag = new Tag { Name = tagName };
                await tagRepository.InsertAsync(tag);
            }

            await documentTagRepository.InsertAsync(new DocumentTag { DocumentId = documentId, TagId = tag.Id });
        }
    }

    private static DocumentDto MapToDto(Document d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        FilePath = d.FilePath,
        Description = d.Description,
        ContentType = d.ContentType,
        FileSize = d.FileSize,
        UploadedByPersonId = d.UploadedByPersonId,
        UploadedByName = $"{d.UploadedBy.GivenNames} {d.UploadedBy.FamilyName}",
        CreatedAtUtc = d.CreatedAtUtc,
        Tags = d.DocumentTags.Select(dt => dt.Tag.Name).ToList()
    };
}