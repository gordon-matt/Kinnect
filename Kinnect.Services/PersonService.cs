using System.Text.Json;
using Ardalis.Result;
using Extenso.Data.Entity;
using Kinnect.Data.Entities;
using Kinnect.Models;
using Kinnect.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Kinnect.Services;

public class PersonService(IRepository<Person> personRepository, IRepository<PersonSpouse> spouseRepository, IRepository<PersonVersion> versionRepository) : IPersonService
{
    public async Task<Result<IEnumerable<PersonDto>>> GetAllAsync()
    {
        var people = await personRepository.FindAsync(new SearchOptions<Person>());
        return Result.Success(people.Select(MapToDto));
    }

    public async Task<Result<PersonDto>> GetByIdAsync(int id)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
            return Result.NotFound("Person not found.");

        return Result.Success(MapToDto(person));
    }

    public async Task<Result<PersonDto>> GetByUserIdAsync(string userId)
    {
        var people = await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = x => x.UserId == userId
        });
        var person = people.FirstOrDefault();

        if (person is null)
            return Result.NotFound("No person record linked to this user.");

        return Result.Success(MapToDto(person));
    }

    public async Task<Result<PersonDto>> CreateAsync(PersonEditRequest request, string? userId = null)
    {
        var person = new Person
        {
            FamilyName = request.FamilyName,
            GivenNames = request.GivenNames,
            IsMale = request.IsMale,
            YearOfBirth = request.YearOfBirth,
            MonthOfBirth = request.MonthOfBirth,
            DayOfBirth = request.DayOfBirth,
            YearOfDeath = request.YearOfDeath,
            MonthOfDeath = request.MonthOfDeath,
            DayOfDeath = request.DayOfDeath,
            PlaceOfBirth = request.PlaceOfBirth,
            PlaceOfDeath = request.PlaceOfDeath,
            Bio = request.Bio,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            FatherId = request.FatherId,
            MotherId = request.MotherId,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await personRepository.InsertAsync(person);
        return Result.Success(MapToDto(person));
    }

    public async Task<Result<PersonDto>> UpdateAsync(int id, PersonEditRequest request, string currentUserId)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
            return Result.NotFound("Person not found.");

        if (person.UserId != null && person.UserId != currentUserId)
            return Result.Forbidden();

        await SaveVersionAsync(person, currentUserId);

        person.FamilyName = request.FamilyName;
        person.GivenNames = request.GivenNames;
        person.IsMale = request.IsMale;
        person.YearOfBirth = request.YearOfBirth;
        person.MonthOfBirth = request.MonthOfBirth;
        person.DayOfBirth = request.DayOfBirth;
        person.YearOfDeath = request.YearOfDeath;
        person.MonthOfDeath = request.MonthOfDeath;
        person.DayOfDeath = request.DayOfDeath;
        person.PlaceOfBirth = request.PlaceOfBirth;
        person.PlaceOfDeath = request.PlaceOfDeath;
        person.Bio = request.Bio;
        person.Latitude = request.Latitude;
        person.Longitude = request.Longitude;
        person.FatherId = request.FatherId;
        person.MotherId = request.MotherId;
        person.UpdatedAtUtc = DateTime.UtcNow;

        await personRepository.UpdateAsync(person);
        return Result.Success(MapToDto(person));
    }

    public async Task<Result> UpdateProfileImageAsync(int id, string imagePath, string currentUserId)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
            return Result.NotFound("Person not found.");

        if (person.UserId != null && person.UserId != currentUserId)
            return Result.Forbidden();

        person.ProfileImagePath = imagePath;
        person.UpdatedAtUtc = DateTime.UtcNow;
        await personRepository.UpdateAsync(person);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
            return Result.NotFound("Person not found.");

        await personRepository.DeleteAsync(person);
        return Result.Success();
    }

    public async Task<Result<IEnumerable<FamilyTreeDatum>>> GetFamilyTreeDataAsync()
    {
        var people = await personRepository.FindAsync(new SearchOptions<Person>());
        var spouses = await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>());
        var peopleList = people.ToList();
        var spouseList = spouses.ToList();

        var data = peopleList.Select(p =>
        {
            var parentIds = new List<string>();
            if (p.FatherId.HasValue) parentIds.Add(p.FatherId.Value.ToString());
            if (p.MotherId.HasValue) parentIds.Add(p.MotherId.Value.ToString());

            var childIds = peopleList
                .Where(c => c.FatherId == p.Id || c.MotherId == p.Id)
                .Select(c => c.Id.ToString())
                .ToList();

            var spouseIds = spouseList
                .Where(s => s.PersonId == p.Id || s.SpouseId == p.Id)
                .Select(s => s.PersonId == p.Id ? s.SpouseId.ToString() : s.PersonId.ToString())
                .Distinct()
                .ToList();

            string? birthday = null;
            if (p.YearOfBirth.HasValue)
            {
                birthday = p.MonthOfBirth.HasValue && p.DayOfBirth.HasValue
                    ? $"{p.YearOfBirth:D4}-{p.MonthOfBirth:D2}-{p.DayOfBirth:D2}"
                    : p.YearOfBirth.Value.ToString();
            }

            return new FamilyTreeDatum
            {
                Id = p.Id.ToString(),
                Data = new FamilyTreeData
                {
                    Gender = p.IsMale ? "M" : "F",
                    FirstName = p.GivenNames,
                    LastName = p.FamilyName,
                    Birthday = birthday,
                    Avatar = p.ProfileImagePath,
                    PersonId = p.Id,
                    HasAccount = p.UserId != null
                },
                Rels = new FamilyTreeRels
                {
                    Parents = parentIds,
                    Children = childIds,
                    Spouses = spouseIds
                }
            };
        });

        return Result.Success(data);
    }

    public async Task<Result> AddSpouseAsync(int personId, int spouseId)
    {
        if (personId == spouseId)
            return Result.Invalid(new ValidationError("Cannot add self as spouse."));

        int lowId = Math.Min(personId, spouseId);
        int highId = Math.Max(personId, spouseId);

        bool exists = await spouseRepository.ExistsAsync(x => x.PersonId == lowId && x.SpouseId == highId);

        if (exists)
            return Result.Conflict("Spouse relationship already exists.");

        await spouseRepository.InsertAsync(new PersonSpouse { PersonId = lowId, SpouseId = highId });
        return Result.Success();
    }

    public async Task<Result> RemoveSpouseAsync(int personId, int spouseId)
    {
        int lowId = Math.Min(personId, spouseId);
        int highId = Math.Max(personId, spouseId);

        var spouses = await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>
        {
            Query = x => x.PersonId == lowId && x.SpouseId == highId
        });
        var spouse = spouses.FirstOrDefault();

        if (spouse is null)
            return Result.NotFound("Spouse relationship not found.");

        await spouseRepository.DeleteAsync(spouse);
        return Result.Success();
    }

    public async Task<Result<IEnumerable<MapPinDto>>> GetMapPinsAsync()
    {
        var people = await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = x => x.Latitude != null && x.Longitude != null && x.YearOfDeath == null
        });

        var pins = people.Select(p => new MapPinDto
        {
            PersonId = p.Id,
            FullName = $"{p.GivenNames} {p.FamilyName}",
            ProfileImagePath = p.ProfileImagePath,
            Latitude = p.Latitude!.Value,
            Longitude = p.Longitude!.Value
        });

        return Result.Success(pins);
    }

    public async Task<Result<IEnumerable<PersonVersionDto>>> GetVersionsAsync(int personId)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
            return Result.NotFound("Person not found.");

        var versions = await versionRepository.FindAsync(new SearchOptions<PersonVersion>
        {
            Query = x => x.PersonId == personId
        });

        return Result.Success(versions.OrderByDescending(v => v.CreatedAtUtc).Select(v => new PersonVersionDto
        {
            Id = v.Id,
            PersonId = v.PersonId,
            VersionData = v.VersionData,
            ChangedByUserId = v.ChangedByUserId,
            CreatedAtUtc = v.CreatedAtUtc
        }));
    }

    public async Task<Result> RestoreVersionAsync(int personId, int versionId, string currentUserId)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
            return Result.NotFound("Person not found.");

        var versions = await versionRepository.FindAsync(new SearchOptions<PersonVersion>
        {
            Query = x => x.Id == versionId && x.PersonId == personId
        });
        var version = versions.FirstOrDefault();

        if (version is null)
            return Result.NotFound("Version not found.");

        await SaveVersionAsync(person, currentUserId);

        var snapshot = JsonSerializer.Deserialize<PersonEditRequest>(version.VersionData);
        if (snapshot is null)
            return Result.Error("Failed to deserialize version data.");

        person.FamilyName = snapshot.FamilyName;
        person.GivenNames = snapshot.GivenNames;
        person.IsMale = snapshot.IsMale;
        person.YearOfBirth = snapshot.YearOfBirth;
        person.MonthOfBirth = snapshot.MonthOfBirth;
        person.DayOfBirth = snapshot.DayOfBirth;
        person.YearOfDeath = snapshot.YearOfDeath;
        person.MonthOfDeath = snapshot.MonthOfDeath;
        person.DayOfDeath = snapshot.DayOfDeath;
        person.PlaceOfBirth = snapshot.PlaceOfBirth;
        person.PlaceOfDeath = snapshot.PlaceOfDeath;
        person.Bio = snapshot.Bio;
        person.Latitude = snapshot.Latitude;
        person.Longitude = snapshot.Longitude;
        person.FatherId = snapshot.FatherId;
        person.MotherId = snapshot.MotherId;
        person.UpdatedAtUtc = DateTime.UtcNow;

        await personRepository.UpdateAsync(person);
        return Result.Success();
    }

    private async Task SaveVersionAsync(Person person, string currentUserId)
    {
        var snapshot = new PersonEditRequest
        {
            FamilyName = person.FamilyName,
            GivenNames = person.GivenNames,
            IsMale = person.IsMale,
            YearOfBirth = person.YearOfBirth,
            MonthOfBirth = person.MonthOfBirth,
            DayOfBirth = person.DayOfBirth,
            YearOfDeath = person.YearOfDeath,
            MonthOfDeath = person.MonthOfDeath,
            DayOfDeath = person.DayOfDeath,
            PlaceOfBirth = person.PlaceOfBirth,
            PlaceOfDeath = person.PlaceOfDeath,
            Bio = person.Bio,
            Latitude = person.Latitude,
            Longitude = person.Longitude,
            FatherId = person.FatherId,
            MotherId = person.MotherId
        };

        await versionRepository.InsertAsync(new PersonVersion
        {
            PersonId = person.Id,
            VersionData = JsonSerializer.Serialize(snapshot),
            ChangedByUserId = currentUserId,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static PersonDto MapToDto(Person p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        FamilyName = p.FamilyName,
        GivenNames = p.GivenNames,
        IsMale = p.IsMale,
        YearOfBirth = p.YearOfBirth,
        MonthOfBirth = p.MonthOfBirth,
        DayOfBirth = p.DayOfBirth,
        YearOfDeath = p.YearOfDeath,
        MonthOfDeath = p.MonthOfDeath,
        DayOfDeath = p.DayOfDeath,
        PlaceOfBirth = p.PlaceOfBirth,
        PlaceOfDeath = p.PlaceOfDeath,
        Bio = p.Bio,
        ProfileImagePath = p.ProfileImagePath,
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        FatherId = p.FatherId,
        MotherId = p.MotherId
    };
}
