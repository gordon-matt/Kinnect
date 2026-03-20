using Ardalis.Result;
using Kinnect.Models;

namespace Kinnect.Services.Abstractions;

public interface IFeedService
{
    Task<Result<IEnumerable<FeedItemDto>>> GetFeedAsync(int page = 1, int pageSize = 20);
}