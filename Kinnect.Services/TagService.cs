namespace Kinnect.Services;

public class TagService(IRepository<Tag> tagRepository) : ITagService
{
    public async Task<Result<IEnumerable<TagDto>>> GetAllAsync()
    {
        var tags = await tagRepository.FindAsync(new SearchOptions<Tag>());
        return Result.Success(tags.OrderBy(t => t.Name).Select(t => new TagDto { Id = t.Id, Name = t.Name }));
    }

    public async Task<Result<IEnumerable<TagDto>>> SearchAsync(string query)
    {
        var tags = await tagRepository.FindAsync(new SearchOptions<Tag>
        {
            Query = x => x.Name.Contains(query)
        });

        return Result.Success(tags.OrderBy(t => t.Name).Select(t => new TagDto { Id = t.Id, Name = t.Name }));
    }
}