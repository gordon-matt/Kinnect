using System.Text.Json;

namespace Kinnect.Services;

public class PersonService(
    IRepository<Person> personRepository,
    IRepository<PersonSpouse> spouseRepository,
    IRepository<PersonVersion> versionRepository,
    IRepository<PersonEvent> eventRepository) : IPersonService
{
    public async Task<Result> AddSpouseAsync(int personId, int spouseId)
    {
        if (personId == spouseId)
        {
            return Result.Invalid(new ValidationError("Cannot add self as spouse."));
        }

        int lowId = Math.Min(personId, spouseId);
        int highId = Math.Max(personId, spouseId);

        bool exists = await spouseRepository.ExistsAsync(x => x.PersonId == lowId && x.SpouseId == highId);

        if (exists)
        {
            return Result.Conflict("Spouse relationship already exists.");
        }

        await spouseRepository.InsertAsync(new PersonSpouse { PersonId = lowId, SpouseId = highId });
        return Result.Success();
    }

    public async Task<Result<PersonDto>> CreateAsync(PersonEditRequest request, string? userId = null)
    {
        var person = new Person
        {
            FamilyName = request.FamilyName,
            GivenNames = request.GivenNames,
            IsMale = request.IsMale,
            Bio = request.Bio,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            FatherId = request.FatherId,
            MotherId = request.MotherId,
            Occupation = request.Occupation,
            Education = request.Education,
            Religion = request.Religion,
            Note = request.Note,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await personRepository.InsertAsync(person);
        return Result.Success(MapToDto(person));
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        await personRepository.DeleteAsync(person);
        return Result.Success();
    }

    public async Task<Result<IEnumerable<PersonDto>>> GetAllAsync()
    {
        var people = await personRepository.FindAsync(new SearchOptions<Person>());
        return Result.Success(people.Select(MapToDto));
    }

    public async Task<Result<PersonDto>> GetByIdAsync(int id)
    {
        var person = await personRepository.FindOneAsync(id);
        return person is null ? (Result<PersonDto>)Result.NotFound("Person not found.") : Result.Success(MapToDto(person));
    }

    public async Task<Result<PersonDto>> GetByUserIdAsync(string userId)
    {
        var people = await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = x => x.UserId == userId
        });
        var person = people.FirstOrDefault();

        return person is null ? (Result<PersonDto>)Result.NotFound("No person record linked to this user.") : Result.Success(MapToDto(person));
    }

    public async Task<Result<IEnumerable<FamilyTreeDatum>>> GetFamilyTreeDataAsync()
    {
        var people = await personRepository.FindAsync(new SearchOptions<Person>());
        var spouses = await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>());
        var allEvents = await eventRepository.FindAsync(new SearchOptions<PersonEvent>());
        var birthByPerson = allEvents
            .Where(e => e.EventType == PersonEventType.Birth)
            .GroupBy(e => e.PersonId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Year ?? 9999).ThenBy(x => x.Month ?? 0).ThenBy(x => x.Day ?? 0).First());

        var peopleList = people.ToList();
        var spouseList = spouses.ToList();

        var data = peopleList.Select(p =>
        {
            var parentIds = new List<string>();
            if (p.FatherId.HasValue)
            {
                parentIds.Add(p.FatherId.Value.ToString());
            }

            if (p.MotherId.HasValue)
            {
                parentIds.Add(p.MotherId.Value.ToString());
            }

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
            if (birthByPerson.TryGetValue(p.Id, out var birt) && birt.Year.HasValue)
            {
                birthday = birt.Month.HasValue && birt.Day.HasValue
                    ? $"{birt.Year:D4}-{birt.Month:D2}-{birt.Day:D2}"
                    : birt.Year.Value.ToString();
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

    public async Task<Result<IEnumerable<MapPinDto>>> GetMapPinsAsync()
    {
        var deadIds = (await eventRepository.FindAsync(new SearchOptions<PersonEvent>
        {
            Query = x => x.EventType == PersonEventType.Death
        })).Select(e => e.PersonId).ToHashSet();

        var people = await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = x => x.Latitude != null && x.Longitude != null
        });

        var pins = people
            .Where(p => !deadIds.Contains(p.Id))
            .Select(p => new MapPinDto
            {
                PersonId = p.Id,
                FullName = $"{p.GivenNames} {p.FamilyName}",
                ProfileImagePath = p.ProfileImagePath,
                Latitude = p.Latitude!.Value,
                Longitude = p.Longitude!.Value
            });

        return Result.Success(pins);
    }

    public async Task<Result<IEnumerable<PersonSpouseDetailDto>>> GetSpousesForPersonAsync(int personId)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        var links = await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>
        {
            Query = x => x.PersonId == personId || x.SpouseId == personId
        });

        var allPeople = (await personRepository.FindAsync(new SearchOptions<Person>())).ToDictionary(p => p.Id);

        var dtos = links.Select(link =>
        {
            int otherId = link.PersonId == personId ? link.SpouseId : link.PersonId;
            return !allPeople.TryGetValue(otherId, out var other)
                ? null
                : new PersonSpouseDetailDto
                {
                    SpousePersonId = otherId,
                    GivenNames = other.GivenNames,
                    FamilyName = other.FamilyName,
                    MarriageYear = link.MarriageYear,
                    MarriageMonth = link.MarriageMonth,
                    MarriageDay = link.MarriageDay,
                    DivorceYear = link.DivorceYear,
                    DivorceMonth = link.DivorceMonth,
                    DivorceDay = link.DivorceDay,
                    EngagementYear = link.EngagementYear,
                    EngagementMonth = link.EngagementMonth,
                    EngagementDay = link.EngagementDay
                };
        }).Where(x => x != null).Cast<PersonSpouseDetailDto>()
            .OrderBy(s => s.FamilyName).ThenBy(s => s.GivenNames)
            .ToList();

        return Result.Success<IEnumerable<PersonSpouseDetailDto>>(dtos);
    }

    public async Task<Result<IEnumerable<PersonVersionDto>>> GetVersionsAsync(int personId)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

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

    public async Task<Result> LinkUserAccountAsync(int personId, string userId)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        var existing = await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = x => x.UserId == userId
        });
        if (existing.Any())
        {
            return Result.Conflict("This user account is already linked to another person.");
        }

        person.UserId = userId;
        person.UpdatedAtUtc = DateTime.UtcNow;
        await personRepository.UpdateAsync(person);
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
        {
            return Result.NotFound("Spouse relationship not found.");
        }

        await spouseRepository.DeleteAsync(spouse);
        return Result.Success();
    }

    public async Task<Result> RestoreVersionAsync(int personId, int versionId, string currentUserId)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        var versions = await versionRepository.FindAsync(new SearchOptions<PersonVersion>
        {
            Query = x => x.Id == versionId && x.PersonId == personId
        });
        var version = versions.FirstOrDefault();

        if (version is null)
        {
            return Result.NotFound("Version not found.");
        }

        await SaveVersionAsync(person, currentUserId);

        var snapshot = JsonSerializer.Deserialize<PersonEditRequest>(version.VersionData);
        if (snapshot is null)
        {
            return Result.Error("Failed to deserialize version data.");
        }

        person.FamilyName = snapshot.FamilyName;
        person.GivenNames = snapshot.GivenNames;
        person.IsMale = snapshot.IsMale;
        person.Bio = snapshot.Bio;
        person.Latitude = snapshot.Latitude;
        person.Longitude = snapshot.Longitude;
        person.FatherId = snapshot.FatherId;
        person.MotherId = snapshot.MotherId;
        person.Occupation = snapshot.Occupation;
        person.Education = snapshot.Education;
        person.Religion = snapshot.Religion;
        person.Note = snapshot.Note;
        person.UpdatedAtUtc = DateTime.UtcNow;

        await personRepository.UpdateAsync(person);
        return Result.Success();
    }

    public async Task<Result> UnlinkUserAccountAsync(int personId)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        person.UserId = null;
        person.UpdatedAtUtc = DateTime.UtcNow;
        await personRepository.UpdateAsync(person);
        return Result.Success();
    }

    public async Task<Result<PersonDto>> UpdateAsync(int id, PersonEditRequest request, string currentUserId, bool isAdmin = false)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        if (!isAdmin && person.UserId != null && person.UserId != currentUserId)
        {
            return Result.Forbidden();
        }

        await SaveVersionAsync(person, currentUserId);

        person.FamilyName = request.FamilyName;
        person.GivenNames = request.GivenNames;
        person.IsMale = request.IsMale;
        person.Bio = request.Bio;
        person.Latitude = request.Latitude;
        person.Longitude = request.Longitude;
        person.FatherId = request.FatherId;
        person.MotherId = request.MotherId;
        person.Occupation = request.Occupation;
        person.Education = request.Education;
        person.Religion = request.Religion;
        person.Note = request.Note;
        person.UpdatedAtUtc = DateTime.UtcNow;

        await personRepository.UpdateAsync(person);
        return Result.Success(MapToDto(person));
    }

    public async Task<Result> UpdateParentsAsync(int id, int? fatherId, int? motherId, string currentUserId, bool isAdmin = false)
    {
        if (fatherId.HasValue && fatherId == id)
        {
            return Result.Invalid(new ValidationError("Father cannot be the same person."));
        }

        if (motherId.HasValue && motherId == id)
        {
            return Result.Invalid(new ValidationError("Mother cannot be the same person."));
        }

        if (fatherId.HasValue && motherId.HasValue && fatherId == motherId)
        {
            return Result.Invalid(new ValidationError("Father and mother must be different people."));
        }

        var person = await personRepository.FindOneAsync(id);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        if (!isAdmin && person.UserId != null && person.UserId != currentUserId)
        {
            return Result.Forbidden();
        }

        await SaveVersionAsync(person, currentUserId);

        person.FatherId = fatherId;
        person.MotherId = motherId;
        person.UpdatedAtUtc = DateTime.UtcNow;

        await personRepository.UpdateAsync(person);
        return Result.Success();
    }

    public async Task<Result> UpdateProfileImageAsync(int id, string imagePath, string currentUserId, bool isAdmin = false)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        if (!isAdmin && person.UserId != null && person.UserId != currentUserId)
        {
            return Result.Forbidden();
        }

        person.ProfileImagePath = imagePath;
        person.UpdatedAtUtc = DateTime.UtcNow;
        await personRepository.UpdateAsync(person);
        return Result.Success();
    }

    public async Task<Result> UpdateSpouseRelationshipAsync(
        int personId,
        int spouseId,
        PersonSpouseUpdateRequest request,
        string currentUserId,
        bool isAdmin = false)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        if (!isAdmin && person.UserId != null && person.UserId != currentUserId)
        {
            return Result.Forbidden();
        }

        int lowId = Math.Min(personId, spouseId);
        int highId = Math.Max(personId, spouseId);

        var matches = await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>
        {
            Query = x => x.PersonId == lowId && x.SpouseId == highId
        });
        var link = matches.FirstOrDefault();
        if (link is null)
        {
            return Result.NotFound("Spouse relationship not found.");
        }

        link.MarriageYear = request.MarriageYear;
        link.MarriageMonth = request.MarriageMonth;
        link.MarriageDay = request.MarriageDay;
        link.DivorceYear = request.DivorceYear;
        link.DivorceMonth = request.DivorceMonth;
        link.DivorceDay = request.DivorceDay;
        link.EngagementYear = request.EngagementYear;
        link.EngagementMonth = request.EngagementMonth;
        link.EngagementDay = request.EngagementDay;

        await spouseRepository.UpdateAsync(link);
        return Result.Success();
    }

    private static PersonDto MapToDto(Person p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        FamilyName = p.FamilyName,
        GivenNames = p.GivenNames,
        IsMale = p.IsMale,
        Bio = p.Bio,
        ProfileImagePath = p.ProfileImagePath,
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        FatherId = p.FatherId,
        MotherId = p.MotherId,
        Occupation = p.Occupation,
        Education = p.Education,
        Religion = p.Religion,
        Note = p.Note,
        GedcomId = p.GedcomId
    };

    private async Task SaveVersionAsync(Person person, string currentUserId)
    {
        var snapshot = new PersonEditRequest
        {
            FamilyName = person.FamilyName,
            GivenNames = person.GivenNames,
            IsMale = person.IsMale,
            Bio = person.Bio,
            Latitude = person.Latitude,
            Longitude = person.Longitude,
            FatherId = person.FatherId,
            MotherId = person.MotherId,
            Occupation = person.Occupation,
            Education = person.Education,
            Religion = person.Religion,
            Note = person.Note
        };

        await versionRepository.InsertAsync(new PersonVersion
        {
            PersonId = person.Id,
            VersionData = JsonSerializer.Serialize(snapshot),
            ChangedByUserId = currentUserId,
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}