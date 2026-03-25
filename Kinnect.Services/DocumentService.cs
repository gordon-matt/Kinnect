namespace Kinnect.Services;

public class DocumentService(
    IRepository<Document> documentRepository,
    IRepository<Tag> tagRepository,
    IRepository<DocumentTag> documentTagRepository) : IDocumentService
{
    public async Task<Result<DocumentDto>> CreateAsync(
        string title,
        string? description,
        string filePath,
        string contentType,
        long fileSize,
        int uploadedByPersonId,
        List<string>? tags)
    {
        var document = await documentRepository.InsertAsync(new Document
        {
            Title = title,
            Description = description,
            FilePath = filePath,
            ContentType = contentType,
            FileSize = fileSize,
            UploadedByPersonId = uploadedByPersonId,
            CreatedAtUtc = DateTime.UtcNow
        });

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

    public async Task<Result> DeleteAsync(int id, string currentUserId)
    {
        var document = await documentRepository.FindOneAsync(id);
        if (document is null)
        {
            return Result.NotFound("Document not found.");
        }

        await documentRepository.DeleteAsync(document);
        return Result.Success();
    }

    public async Task<Result<DocumentDto>> GetByIdAsync(int id)
    {
        var document = await documentRepository.FindOneAsync(new SearchOptions<Document>
        {
            Query = x => x.Id == id,
            Include = q => q
                .Include(d => d.UploadedBy)
                .Include(d => d.DocumentTags).ThenInclude(dt => dt.Tag)
        });

        return document is null ? (Result<DocumentDto>)Result.NotFound("Document not found.") : Result.Success(document.ToDto());
    }

    public async Task<Result<IEnumerable<DocumentDto>>> GetByPersonAsync(int personId)
    {
        var documents = await documentRepository.FindAsync(new SearchOptions<Document>
        {
            Query = x => x.UploadedByPersonId == personId,
            Include = q => q
                .Include(d => d.UploadedBy)
                .Include(d => d.DocumentTags).ThenInclude(dt => dt.Tag),
            OrderBy = query => query.OrderByDescending(d => d.CreatedAtUtc)
        });

        return Result.Success(documents.Select(d => d.ToDto()));
    }

    public async Task<Result> UpdateTagsAsync(int id, List<string> tags)
    {
        var document = await documentRepository.FindOneAsync(id);
        if (document is null)
        {
            return Result.NotFound("Document not found.");
        }

        await SyncTagsAsync(id, tags);
        return Result.Success();
    }

    private async Task SyncTagsAsync(int documentId, List<string> tagNames)
    {
        await documentTagRepository.DeleteAsync(x => x.DocumentId == documentId);

        foreach (string tagName in tagNames.Distinct())
        {
            var tag = await tagRepository.FindOneAsync(new SearchOptions<Tag>
            {
                Query = x => x.Name == tagName
            });

            tag ??= await tagRepository.InsertAsync(new Tag { Name = tagName });

            await documentTagRepository.InsertAsync(new DocumentTag { DocumentId = documentId, TagId = tag.Id });
        }
    }
}