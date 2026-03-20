using Ardalis.Result;
using Kinnect.Models;

namespace Kinnect.Services.Abstractions;

public interface ITagService
{
    Task<Result<IEnumerable<TagDto>>> GetAllAsync();
    Task<Result<IEnumerable<TagDto>>> SearchAsync(string query);
}
