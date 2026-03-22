using Ardalis.Result;
using Kinnect.Models;

namespace Kinnect.Services.Abstractions;

public interface IPersonService
{
    Task<Result<IEnumerable<PersonDto>>> GetAllAsync();

    Task<Result<PersonDto>> GetByIdAsync(int id);

    Task<Result<PersonDto>> GetByUserIdAsync(string userId);

    Task<Result<PersonDto>> CreateAsync(PersonEditRequest request, string? userId = null);

    Task<Result<PersonDto>> UpdateAsync(int id, PersonEditRequest request, string currentUserId, bool isAdmin = false);

    Task<Result> UpdateParentsAsync(int id, int? fatherId, int? motherId, string currentUserId, bool isAdmin = false);

    Task<Result> UpdateProfileImageAsync(int id, string imagePath, string currentUserId, bool isAdmin = false);

    Task<Result> DeleteAsync(int id, string currentUserId);

    Task<Result> LinkUserAccountAsync(int personId, string userId);

    Task<Result> UnlinkUserAccountAsync(int personId);

    Task<Result<IEnumerable<FamilyTreeDatum>>> GetFamilyTreeDataAsync();

    Task<Result> AddSpouseAsync(int personId, int spouseId);

    Task<Result> RemoveSpouseAsync(int personId, int spouseId);

    Task<Result<IEnumerable<PersonSpouseDetailDto>>> GetSpousesForPersonAsync(int personId);

    Task<Result> UpdateSpouseRelationshipAsync(int personId, int spouseId, PersonSpouseUpdateRequest request, string currentUserId, bool isAdmin = false);

    Task<Result<IEnumerable<MapPinDto>>> GetMapPinsAsync();

    Task<Result<IEnumerable<PersonVersionDto>>> GetVersionsAsync(int personId);

    Task<Result> RestoreVersionAsync(int personId, int versionId, string currentUserId);
}