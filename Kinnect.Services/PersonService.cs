using System.Text.Json;
using Kinnect.Models.FamilyTree;
using Microsoft.AspNetCore.Identity;

namespace Kinnect.Services;

public class PersonService(
    IRepository<Person> personRepository,
    IRepository<PersonSpouse> spouseRepository,
    IRepository<PersonVersion> versionRepository,
    IRepository<PersonEvent> eventRepository,
    IRepository<PersonLink> linkRepository,
    IRepository<Contact> contactRepository,
    IParentSetService parentSets,
    UserManager<ApplicationUser>? accounts = null,
    ISettingsService? settings = null) : IPersonService
{
    private const int MaxContacts = 20;

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
        var (Errors, Links) = NormalizeLinks(request.Links);
        if (Errors.Count > 0)
        {
            return Result<PersonDto>.Invalid(Errors);
        }
        var contacts = NormalizeContacts(request.Contacts);
        if (contacts.Errors.Count > 0)
        {
            return Result<PersonDto>.Invalid(contacts.Errors);
        }

        bool familyNameFirst = request.IsFamilyNameFirst ?? await DefaultFamilyNameFirstAsync();
        var person = await personRepository.InsertAsync(new Person
        {
            FamilyName = request.FamilyName,
            GivenNames = request.GivenNames,
            NamePrefix = CleanNamePart(request.NamePrefix),
            Nickname = CleanNamePart(request.Nickname),
            NameSuffix = CleanNamePart(request.NameSuffix),
            IsMale = request.IsMale,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Occupation = request.Occupation,
            Education = request.Education,
            Religion = request.Religion,
            Note = request.Note,
            IsDeceased = request.IsDeceased,
            IsFamilyNameFirst = familyNameFirst,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        if (Links is not null)
        {
            await ReplaceLinksAsync(person.Id, Links);
        }

        if (contacts.Contacts is not null)
        {
            await ReplaceContactsAsync(person.Id, contacts.Contacts);
        }

        return Result.Success(await WithDetailsAsync(person));
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        // A parent's memberships are restricted, not cascaded, so they go first. The person's own sets cascade.
        await parentSets.DetachParentAsync(id);
        await personRepository.DeleteAsync(person);
        return Result.Success();
    }

    public async Task<Result<IEnumerable<PersonDto>>> GetAllAsync()
    {
        var people = await personRepository.FindAsync(new SearchOptions<Person>());
        return Result.Success(people.Select(p => p.ToDto()));
    }

    public async Task<Result<PersonDto>> GetByIdAsync(int id)
    {
        var person = await personRepository.FindOneAsync(id);
        return person is null ? (Result<PersonDto>)Result.NotFound("Person not found.") : Result.Success(await WithDetailsAsync(person));
    }

    public async Task<Result<PersonDto>> GetByUserIdAsync(string userId)
    {
        var person = await personRepository.FindOneAsync(new SearchOptions<Person>
        {
            Query = x => x.UserId == userId
        });

        return person is null ? (Result<PersonDto>)Result.NotFound("No person record linked to this user.") : Result.Success(await WithDetailsAsync(person));
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

        // Lineage policy: this follows the primary set of parents the viewer can see (see ParentGraph).
        var lineage = await parentSets.LoadGraphAsync();
        var data = peopleList.Select(p =>
        {
            var parentIds = lineage.ParentsOf(p.Id).Select(id => id.ToString()).ToList();
            var childIds = lineage.ChildrenOf(p.Id).Select(id => id.ToString()).ToList();

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
                    Gender = p.IsMale switch { true => "M", false => "F", _ => "U" },
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
        var people = await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = x =>
                !x.IsDeceased &&
                x.Latitude != null &&
                x.Longitude != null
        });

        var pins = people
            .Select(p => new MapPinDto
            {
                PersonId = p.Id,
                FullName = PersonDisplay.Name(p),
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

        var spouseIds = links
            .Select(link => link.PersonId == personId ? link.SpouseId : link.PersonId)
            .Distinct()
            .ToList();

        if (spouseIds.Count == 0)
        {
            return Result.Success<IEnumerable<PersonSpouseDetailDto>>([]);
        }

        var spouses = (await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = p => spouseIds.Contains(p.Id)
        })).ToDictionary(p => p.Id);

        var dtos = links
            .Select(link =>
            {
                int otherId = link.PersonId == personId ? link.SpouseId : link.PersonId;
                return !spouses.TryGetValue(otherId, out var other)
                    ? null
                    : new PersonSpouseDetailDto
                    {
                        SpousePersonId = otherId,
                        GivenNames = other.GivenNames,
                        FamilyName = other.FamilyName,
                        IsFamilyNameFirst = other.IsFamilyNameFirst,
                        MarriageYear = link.MarriageYear,
                        MarriageMonth = link.MarriageMonth,
                        MarriageDay = link.MarriageDay,
                        DivorceYear = link.DivorceYear,
                        DivorceMonth = link.DivorceMonth,
                        DivorceDay = link.DivorceDay,
                        EngagementYear = link.EngagementYear,
                        EngagementMonth = link.EngagementMonth,
                        EngagementDay = link.EngagementDay,
                        HasEngagement = link.HasEngagement,
                        HasMarriage = link.HasMarriage,
                        HasDivorce = link.HasDivorce
                    };
            })
            .Where(x => x != null)
            .Cast<PersonSpouseDetailDto>()
            .OrderBy(s => s.FamilyName)
            .ThenBy(s => s.GivenNames)
            .ToList();

        return Result.Success<IEnumerable<PersonSpouseDetailDto>>(dtos);
    }

    public async Task<Result<IEnumerable<PersonVersionDto>>> GetVersionsAsync(int personId)
    {
        bool personExists = await personRepository.ExistsAsync(x => x.Id == personId);
        if (!personExists)
        {
            return Result.NotFound("Person not found.");
        }

        var versions = await versionRepository.FindAsync(new SearchOptions<PersonVersion>
        {
            Query = x => x.PersonId == personId,
            OrderBy = query => query.OrderByDescending(v => v.CreatedAtUtc)
        });

        return Result.Success(versions.Select(v => new PersonVersionDto
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

        bool alreadyExists = await personRepository.ExistsAsync(x => x.UserId == userId);
        if (alreadyExists)
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

        var spouse = await spouseRepository.FindOneAsync(new SearchOptions<PersonSpouse>
        {
            Query = x => x.PersonId == lowId && x.SpouseId == highId
        });

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

        var version = await versionRepository.FindOneAsync(new SearchOptions<PersonVersion>
        {
            Query = x => x.Id == versionId && x.PersonId == personId
        });

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
        if (snapshot.IsFamilyNameFirst is bool familyNameFirst)
        {
            person.IsFamilyNameFirst = familyNameFirst;
        }
        person.NamePrefix = CleanNamePart(snapshot.NamePrefix);
        person.Nickname = CleanNamePart(snapshot.Nickname);
        person.NameSuffix = CleanNamePart(snapshot.NameSuffix);
        person.IsMale = snapshot.IsMale;
        person.Latitude = snapshot.Latitude;
        person.Longitude = snapshot.Longitude;
        person.Occupation = snapshot.Occupation;
        person.Education = snapshot.Education;
        person.Religion = snapshot.Religion;
        person.Note = PersonNoteText.Merge(snapshot.LegacyBio, snapshot.Note);
        person.IsDeceased = snapshot.IsDeceased;
        person.UpdatedAtUtc = DateTime.UtcNow;

        await personRepository.UpdateAsync(person);
        return Result.Success();
    }

    public async Task<Result> UnlinkUserAccountAsync(int personId, string? currentUserId)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        string? linkedUserId = person.UserId;
        bool unlinkingSelf = !string.IsNullOrEmpty(currentUserId)
            && string.Equals(linkedUserId, currentUserId, StringComparison.Ordinal);
        person.UserId = null;
        person.UpdatedAtUtc = DateTime.UtcNow;
        await personRepository.UpdateAsync(person);

        // Unlinking someone else is how an administrator withdraws their access. Unlinking yourself
        // has to leave the account usable, so you can choose a different person.
        if (!unlinkingSelf)
        {
            await LockAccountAsync(linkedUserId);
        }

        return Result.Success();
    }

    private async Task LockAccountAsync(string? userId)
    {
        if (accounts is null || string.IsNullOrEmpty(userId))
        {
            return;
        }

        var account = await accounts.FindByIdAsync(userId);
        if (account is null)
        {
            return;
        }

        await accounts.SetLockoutEnabledAsync(account, true);
        await accounts.SetLockoutEndDateAsync(account, DateTimeOffset.MaxValue);
    }

    public async Task<Result<PersonDto>> UpdateAsync(int id, PersonEditRequest request, string currentUserId, bool isAdmin = false, bool isEditorOrAbove = false)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        bool canEdit = person.UserId == currentUserId
            || isAdmin
            || (isEditorOrAbove && person.UserId is null);

        if (!canEdit)
        {
            return Result.Forbidden();
        }

        var (Errors, Links) = NormalizeLinks(request.Links);
        if (Errors.Count > 0)
        {
            return Result<PersonDto>.Invalid(Errors);
        }
        var contacts = NormalizeContacts(request.Contacts);
        if (contacts.Errors.Count > 0)
        {
            return Result<PersonDto>.Invalid(contacts.Errors);
        }

        if (person.UserId == currentUserId)
        {
            request.IsDeceased = false;
        }

        await SaveVersionAsync(person, currentUserId);

        person.FamilyName = request.FamilyName;
        person.GivenNames = request.GivenNames;
        if (request.IsFamilyNameFirst is bool familyNameFirst)
        {
            person.IsFamilyNameFirst = familyNameFirst;
        }
        person.NamePrefix = CleanNamePart(request.NamePrefix);
        person.Nickname = CleanNamePart(request.Nickname);
        person.NameSuffix = CleanNamePart(request.NameSuffix);
        person.IsMale = request.IsMale;
        person.Latitude = request.Latitude;
        person.Longitude = request.Longitude;
        person.Occupation = request.Occupation;
        person.Education = request.Education;
        person.Religion = request.Religion;
        person.Note = request.Note;
        person.IsDeceased = request.IsDeceased;
        person.UpdatedAtUtc = DateTime.UtcNow;

        await personRepository.UpdateAsync(person);
        if (request.ApplyFamilyNameOrderToLine)
        {
            await ApplyFamilyNameOrderAsync(person, currentUserId, isAdmin, isEditorOrAbove);
        }

        if (Links is not null)
        {
            await ReplaceLinksAsync(person.Id, Links);
        }

        if (contacts.Contacts is not null)
        {
            await ReplaceContactsAsync(person.Id, contacts.Contacts);
        }

        return Result.Success(await WithDetailsAsync(person));
    }

    public async Task<Result> UpdateProfileImageAsync(int id, string imagePath, string currentUserId, bool isAdmin = false, bool isEditorOrAbove = false)
    {
        var person = await personRepository.FindOneAsync(id);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        bool canEdit = person.UserId == currentUserId
            || isAdmin
            || (isEditorOrAbove && person.UserId is null);

        if (!canEdit)
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
        bool isAdmin = false,
        bool isEditorOrAbove = false)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        bool canEdit = person.UserId == currentUserId
            || isAdmin
            || (isEditorOrAbove && person.UserId is null);

        if (!canEdit)
        {
            return Result.Forbidden();
        }

        int lowId = Math.Min(personId, spouseId);
        int highId = Math.Max(personId, spouseId);

        var link = await spouseRepository.FindOneAsync(new SearchOptions<PersonSpouse>
        {
            Query = x => x.PersonId == lowId && x.SpouseId == highId
        });

        if (link is null)
        {
            return Result.NotFound("Spouse relationship not found.");
        }

        link.HasEngagement = request.HasEngagement;
        link.EngagementYear = request.HasEngagement ? request.EngagementYear : null;
        link.EngagementMonth = request.HasEngagement ? request.EngagementMonth : null;
        link.EngagementDay = request.HasEngagement ? request.EngagementDay : null;
        link.HasMarriage = request.HasMarriage;
        link.MarriageYear = request.HasMarriage ? request.MarriageYear : null;
        link.MarriageMonth = request.HasMarriage ? request.MarriageMonth : null;
        link.MarriageDay = request.HasMarriage ? request.MarriageDay : null;
        link.HasDivorce = request.HasDivorce;
        link.DivorceYear = request.HasDivorce ? request.DivorceYear : null;
        link.DivorceMonth = request.HasDivorce ? request.DivorceMonth : null;
        link.DivorceDay = request.HasDivorce ? request.DivorceDay : null;

        await spouseRepository.UpdateAsync(link);
        return Result.Success();
    }

    private async Task<bool> DefaultFamilyNameFirstAsync()
    {
        if (settings is null)
        {
            return false;
        }

        var tree = await settings.GetAsync<TreeSettings>();
        return tree.DefaultNameOrder == NameOrder.FamilyNameFirst;
    }

    /// <summary>
    /// Copies this person's name order onto direct ancestors and descendants who share the family name
    /// and whom the current user may edit.
    /// </summary>
    private async Task ApplyFamilyNameOrderAsync(Person person, string currentUserId, bool isAdmin, bool isEditorOrAbove)
    {
        string familyName = person.FamilyName.Trim();
        if (familyName.Length == 0)
        {
            return;
        }

        var graph = await parentSets.LoadGraphAsync();
        var people = (await personRepository.FindAsync(new SearchOptions<Person>())).ToDictionary(relative => relative.Id);
        foreach (int relativeId in DirectLine(graph, person.Id))
        {
            if (!people.TryGetValue(relativeId, out var relative)
                || relative.IsFamilyNameFirst == person.IsFamilyNameFirst
                || !string.Equals(relative.FamilyName.Trim(), familyName, StringComparison.OrdinalIgnoreCase)
                || !CanEdit(relative, currentUserId, isAdmin, isEditorOrAbove))
            {
                continue;
            }

            await SaveVersionAsync(relative, currentUserId);
            relative.IsFamilyNameFirst = person.IsFamilyNameFirst;
            relative.UpdatedAtUtc = DateTime.UtcNow;
            await personRepository.UpdateAsync(relative);
        }
    }

    private static bool CanEdit(Person person, string currentUserId, bool isAdmin, bool isEditorOrAbove) =>
        person.UserId == currentUserId || isAdmin || (isEditorOrAbove && person.UserId is null);

    private static IEnumerable<int> DirectLine(ParentGraph graph, int personId)
    {
        var seen = new HashSet<int> { personId };
        var next = new Queue<int>();
        next.Enqueue(personId);
        while (next.Count > 0)
        {
            int current = next.Dequeue();
            foreach (int parentId in graph.ParentsOf(current, ParentLineage.Any))
            {
                if (seen.Add(parentId))
                {
                    next.Enqueue(parentId);
                    yield return parentId;
                }
            }
        }

        seen.Clear();
        seen.Add(personId);
        next.Clear();
        next.Enqueue(personId);
        while (next.Count > 0)
        {
            int current = next.Dequeue();
            foreach (int childId in graph.ChildrenOf(current, ParentLineage.Any))
            {
                if (seen.Add(childId))
                {
                    next.Enqueue(childId);
                    yield return childId;
                }
            }
        }
    }

    private async Task SaveVersionAsync(Person person, string currentUserId)
    {
        var snapshot = new PersonEditRequest
        {
            FamilyName = person.FamilyName,
            GivenNames = person.GivenNames,
            IsFamilyNameFirst = person.IsFamilyNameFirst,
            NamePrefix = person.NamePrefix,
            Nickname = person.Nickname,
            NameSuffix = person.NameSuffix,
            IsMale = person.IsMale,
            Latitude = person.Latitude,
            Longitude = person.Longitude,
            Occupation = person.Occupation,
            Education = person.Education,
            Religion = person.Religion,
            Note = person.Note,
            IsDeceased = person.IsDeceased
        };

        await versionRepository.InsertAsync(new PersonVersion
        {
            PersonId = person.Id,
            VersionData = JsonSerializer.Serialize(snapshot),
            ChangedByUserId = currentUserId,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task<PersonDto> WithDetailsAsync(Person person)
    {
        var dto = person.ToDto();
        var stored = await linkRepository.FindAsync(new SearchOptions<PersonLink>
        {
            Query = link => link.PersonId == person.Id
        });

        dto.Links = stored
            .OrderBy(link => link.SortOrder)
            .ThenBy(link => link.Id)
            .Select(link => new PersonLinkDto
            {
                Id = link.Id,
                Name = link.Name,
                Url = link.Url
            })
            .ToList();

        var storedContacts = await contactRepository.FindAsync(new SearchOptions<Contact>
        {
            Query = contact => contact.PersonId == person.Id
        });

        dto.Contacts = storedContacts
            .OrderBy(contact => contact.SortOrder)
            .ThenBy(contact => contact.Id)
            .Select(contact => new ContactDto
            {
                Id = contact.Id,
                Type = contact.Type,
                Value = contact.Value
            })
            .ToList();

        return dto;
    }

    private async Task ReplaceLinksAsync(int personId, IReadOnlyList<PersonLink> links)
    {
        var existing = await linkRepository.FindAsync(new SearchOptions<PersonLink>
        {
            Query = link => link.PersonId == personId
        });

        foreach (var link in existing)
        {
            await linkRepository.DeleteAsync(link);
        }

        foreach (var link in links)
        {
            link.PersonId = personId;
            await linkRepository.InsertAsync(link);
        }
    }

    private async Task ReplaceContactsAsync(int personId, IReadOnlyList<Contact> contacts)
    {
        var existing = await contactRepository.FindAsync(new SearchOptions<Contact>
        {
            Query = contact => contact.PersonId == personId
        });

        foreach (var contact in existing)
        {
            await contactRepository.DeleteAsync(contact);
        }

        foreach (var contact in contacts)
        {
            contact.PersonId = personId;
            await contactRepository.InsertAsync(contact);
        }
    }

    /// <summary>
    /// Null input means "leave the stored contacts alone". Name and value are trimmed before they are checked
    /// or saved, and a row with both left empty is dropped.
    /// </summary>
    private static (List<ValidationError> Errors, List<Contact>? Contacts) NormalizeContacts(IReadOnlyList<ContactInput>? inputs)
    {
        if (inputs is null)
        {
            return ([], null);
        }

        var errors = new List<ValidationError>();
        var contacts = new List<Contact>();
        int order = 0;

        foreach (var input in inputs)
        {
            string value = input.Value?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                continue;
            }

            if (!Enum.IsDefined(input.Type))
            {
                errors.Add(new ValidationError("Choose a contact type."));
            }

            if (value.Length > Contact.MaxValueLength)
            {
                errors.Add(new ValidationError($"Contact values can be at most {Contact.MaxValueLength} characters."));
            }

            contacts.Add(new Contact
            {
                Type = input.Type,
                Value = value,
                SortOrder = order++
            });
        }

        if (contacts.Count > MaxContacts)
        {
            errors.Add(new ValidationError($"A profile can have at most {MaxContacts} contacts."));
        }

        return (errors, contacts);
    }

    /// <summary>Null input means "leave the stored links alone".</summary>
    private static (List<ValidationError> Errors, List<PersonLink>? Links) NormalizeLinks(IReadOnlyList<PersonLinkInput>? inputs)
    {
        if (inputs is null)
        {
            return ([], null);
        }

        var errors = new List<ValidationError>();
        var links = new List<PersonLink>();
        int order = 0;

        foreach (var input in inputs)
        {
            string name = input.Name?.Trim() ?? string.Empty;
            string url = input.Url?.Trim() ?? string.Empty;
            if (name.Length == 0 && url.Length == 0)
            {
                continue;
            }

            if (name.Length == 0)
            {
                errors.Add(new ValidationError("Give the link a name."));
            }
            else if (name.Length > PersonLink.MaxNameLength)
            {
                errors.Add(new ValidationError($"Link names can be at most {PersonLink.MaxNameLength} characters."));
            }

            if (url.Length > PersonLink.MaxUrlLength
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add(new ValidationError("Links need an http or https address."));
            }

            links.Add(new PersonLink
            {
                Name = name,
                Url = url,
                SortOrder = order++
            });
        }

        if (links.Count > 30)
        {
            errors.Add(new ValidationError("A profile can have at most 30 links."));
        }

        return (errors, links);
    }

    private static string? CleanNamePart(string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? null : trimmed.Length <= 100 ? trimmed : trimmed[..100];
    }
}