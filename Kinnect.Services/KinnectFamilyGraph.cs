using MyWebTree;
using MyWebTree.Models;

namespace Kinnect.Services;

/// <summary>
/// Loads the Kinnect people graph into <see cref="PersonData"/> and writes one
/// <see cref="FamilyTreeChange"/> back through the person and event services.
/// Birth and death stay as person events, which is how Kinnect already stores them.
/// </summary>
public sealed class KinnectFamilyGraph(
    IPersonService people,
    IPersonEventService events,
    IRepository<Person> personRepository,
    IRepository<PersonEvent> eventRepository,
    IRepository<PersonSpouse> spouseRepository,
    IUserContextService userContext)
{
    public async Task<FamilyGraphSnapshot> LoadAsync()
    {
        var stored = (await personRepository.FindAsync(new SearchOptions<Person>())).ToList();
        var vitals = (await eventRepository.FindAsync(new SearchOptions<PersonEvent>()))
            .Where(e => e.EventType is PersonEventType.Birth or PersonEventType.Death)
            .ToList();
        var spouses = (await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>())).ToList();

        var byId = stored.ToDictionary(p => p.Id);
        var cards = new List<PersonData>(stored.Count);
        foreach (var person in stored)
        {
            var birth = vitals.FirstOrDefault(e => e.PersonId == person.Id && e.EventType == PersonEventType.Birth);
            var death = vitals.FirstOrDefault(e => e.PersonId == person.Id && e.EventType == PersonEventType.Death);
            string id = person.Id.ToString();

            var parents = new List<string>(2);
            if (person.FatherId is int father && byId.ContainsKey(father))
            {
                parents.Add(father.ToString());
            }

            if (person.MotherId is int mother && byId.ContainsKey(mother))
            {
                parents.Add(mother.ToString());
            }

            var spouseIds = spouses
                .Where(s => s.PersonId == person.Id || s.SpouseId == person.Id)
                .Select(s => s.PersonId == person.Id ? s.SpouseId : s.PersonId)
                .Where(byId.ContainsKey)
                .Select(s => s.ToString())
                .Distinct()
                .ToList();

            var children = stored
                .Where(p => p.FatherId == person.Id || p.MotherId == person.Id)
                .Select(p => p.Id.ToString())
                .ToList();

            cards.Add(new PersonData
            {
                Id = id,
                Gender = person.IsMale ? Gender.Male : Gender.Female,
                FirstName = person.GivenNames,
                LastName = person.FamilyName,
                BirthDate = ToPartial(birth),
                DeathDate = ToPartial(death),
                BirthPlace = birth?.Place,
                DeathPlace = death?.Place,
                AvatarUrl = AvatarUrl(person.ProfileImagePath),
                Notes = person.Note,
                Parents = parents,
                Spouses = spouseIds,
                Children = children,
            });
        }

        string? focus = null;
        string? userId = userContext.GetCurrentUserId();
        if (userId is not null)
        {
            focus = stored.FirstOrDefault(p => p.UserId == userId)?.Id.ToString();
        }

        return new FamilyGraphSnapshot(cards, focus);
    }

    public async Task<Result> ApplyAsync(FamilyTreeChange change)
    {
        if (change.Kind == FamilyTreeChangeKind.FocusChanged)
        {
            return Result.Success();
        }

        if (!userContext.IsAdmin() && !userContext.IsEditor())
        {
            return Result.Forbidden();
        }

        string? userId = userContext.GetCurrentUserId();
        if (userId is null)
        {
            return Result.Unauthorized();
        }

        bool isAdmin = userContext.IsAdmin();
        bool isEditor = userContext.IsEditor();

        return change.Kind switch
        {
            FamilyTreeChangeKind.Removed => await RemoveAsync(change.PersonId, userId),
            FamilyTreeChangeKind.Unlinked => await UnlinkAsync(change),
            FamilyTreeChangeKind.Updated => await UpdateOneAsync(change, userId, isAdmin, isEditor),
            FamilyTreeChangeKind.Added => await AddOneAsync(change, userId, isAdmin, isEditor),
            _ => await ReconcileAsync(change, userId, isAdmin, isEditor),
        };
    }

    private async Task<Result> RemoveAsync(string? personId, string userId)
    {
        if (!int.TryParse(personId, out int id))
        {
            return Result.Success();
        }

        return await people.DeleteAsync(id, userId);
    }

    private async Task<Result> UnlinkAsync(FamilyTreeChange change)
    {
        if (!int.TryParse(change.PersonId, out int left) || !int.TryParse(change.OtherPersonId, out int right))
        {
            return Result.Invalid(new ValidationError("Those people are not in Kinnect yet."));
        }

        var child = await personRepository.FindOneAsync(left);
        if (child?.FatherId == right || child?.MotherId == right)
        {
            return await people.UpdateParentsAsync(
                left,
                child.FatherId == right ? null : child.FatherId,
                child.MotherId == right ? null : child.MotherId,
                userContext.GetCurrentUserId()!,
                userContext.IsAdmin());
        }

        var parent = await personRepository.FindOneAsync(right);
        if (parent?.FatherId == left || parent?.MotherId == left)
        {
            return await people.UpdateParentsAsync(
                right,
                parent.FatherId == left ? null : parent.FatherId,
                parent.MotherId == left ? null : parent.MotherId,
                userContext.GetCurrentUserId()!,
                userContext.IsAdmin());
        }

        return await people.RemoveSpouseAsync(left, right);
    }

    private async Task<Result> UpdateOneAsync(FamilyTreeChange change, string userId, bool isAdmin, bool isEditor)
    {
        var card = change.People.FirstOrDefault(p => p.Id == change.PersonId);
        if (card is null || !int.TryParse(card.Id, out int id))
        {
            return Result.NotFound("Person not found.");
        }

        var result = await SaveFieldsAsync(card, id, userId, isAdmin, isEditor);
        if (!result.IsSuccess)
        {
            return result;
        }

        return await SyncVitalsAsync(id, card);
    }

    private async Task<Result> AddOneAsync(FamilyTreeChange change, string userId, bool isAdmin, bool isEditor)
    {
        var card = change.People.FirstOrDefault(p => p.Id == change.PersonId);
        if (card is null)
        {
            return Result.NotFound("Person not found.");
        }

        var map = change.People
            .Where(p => int.TryParse(p.Id, out _))
            .ToDictionary(p => p.Id, p => int.Parse(p.Id));

        var created = await people.CreateAsync(ToRequest(card, null, change.People, map));
        if (!created.IsSuccess)
        {
            return Plain(created);
        }

        map[card.Id] = created.Value.Id;
        var linked = await SaveFieldsAsync(card, created.Value.Id, userId, isAdmin, isEditor, change.People, map);
        if (!linked.IsSuccess)
        {
            return linked;
        }

        await SyncVitalsAsync(created.Value.Id, card);
        await SyncSpousesAsync(change.People, map, replace: false);
        return await SyncParentsAsync(change.People, map, userId, isAdmin || isEditor);
    }

    private async Task<Result> ReconcileAsync(FamilyTreeChange change, string userId, bool isAdmin, bool isEditor)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var card in change.People.Where(p => !p.IsPlaceholder))
        {
            if (int.TryParse(card.Id, out int existing))
            {
                map[card.Id] = existing;
                continue;
            }

            var created = await people.CreateAsync(ToRequest(card, null, change.People, map));
            if (!created.IsSuccess)
            {
                return Plain(created);
            }

            map[card.Id] = created.Value.Id;
        }

        foreach (var card in change.People.Where(p => !p.IsPlaceholder))
        {
            var saved = await SaveFieldsAsync(card, map[card.Id], userId, isAdmin, isEditor, change.People, map);
            if (!saved.IsSuccess)
            {
                return saved;
            }

            var vitals = await SyncVitalsAsync(map[card.Id], card);
            if (!vitals.IsSuccess)
            {
                return vitals;
            }
        }

        var spouseResult = await SyncSpousesAsync(change.People, map, replace: true);
        if (!spouseResult.IsSuccess)
        {
            return spouseResult;
        }

        var parentResult = await SyncParentsAsync(change.People, map, userId, isAdmin || isEditor);
        if (!parentResult.IsSuccess)
        {
            return parentResult;
        }

        var keep = map.Values.ToHashSet();
        var stored = await personRepository.FindAsync(new SearchOptions<Person>());
        foreach (var person in stored.Where(p => !keep.Contains(p.Id)))
        {
            var deleted = await people.DeleteAsync(person.Id, userId);
            if (!deleted.IsSuccess)
            {
                return deleted;
            }
        }

        return Result.Success();
    }

    private async Task<Result> SaveFieldsAsync(
        PersonData card,
        int id,
        string userId,
        bool isAdmin,
        bool isEditor,
        IReadOnlyList<PersonData>? cards = null,
        IReadOnlyDictionary<string, int>? ids = null)
    {
        var existing = await personRepository.FindOneAsync(id);
        if (existing is null)
        {
            return Result.NotFound("Person not found.");
        }

        (int? father, int? mother) = ids is null || cards is null
            ? (existing.FatherId, existing.MotherId)
            : ParentsOf(card, cards, ids);

        string? image = ImagePath(card.AvatarUrl) ?? existing.ProfileImagePath;
        var request = new PersonEditRequest
        {
            FamilyName = card.LastName.Trim(),
            GivenNames = string.IsNullOrWhiteSpace(card.FirstName) ? existing.GivenNames : card.FirstName.Trim(),
            IsMale = card.Gender == Gender.Unknown ? existing.IsMale : card.Gender == Gender.Male,
            Bio = existing.Bio,
            Latitude = existing.Latitude,
            Longitude = existing.Longitude,
            FatherId = father,
            MotherId = mother,
            Occupation = existing.Occupation,
            Education = existing.Education,
            Religion = existing.Religion,
            Note = string.IsNullOrWhiteSpace(card.Notes) ? null : card.Notes.Trim(),
            IsDeceased = card.DeathDate is not null,
        };

        var updated = await people.UpdateAsync(id, request, userId, isAdmin, isEditor);
        if (!updated.IsSuccess)
        {
            return Plain(updated);
        }

        if (image != existing.ProfileImagePath && image is not null)
        {
            return await people.UpdateProfileImageAsync(id, image, userId, isAdmin, isEditor);
        }

        return Result.Success();
    }

    private async Task<Result> SyncParentsAsync(
        IReadOnlyList<PersonData> cards,
        IReadOnlyDictionary<string, int> ids,
        string userId,
        bool isAdmin)
    {
        foreach (var card in cards.Where(p => !p.IsPlaceholder && ids.ContainsKey(p.Id)))
        {
            (int? father, int? mother) = ParentsOf(card, cards, ids);
            var result = await people.UpdateParentsAsync(ids[card.Id], father, mother, userId, isAdmin);
            if (!result.IsSuccess && result.Status != ResultStatus.Forbidden)
            {
                return result;
            }
        }

        return Result.Success();
    }

    private async Task<Result> SyncSpousesAsync(IReadOnlyList<PersonData> cards, IReadOnlyDictionary<string, int> ids, bool replace)
    {
        var desired = new HashSet<(int Low, int High)>();
        foreach (var card in cards.Where(p => !p.IsPlaceholder && ids.ContainsKey(p.Id)))
        {
            foreach (string spouseId in card.Spouses)
            {
                if (!ids.TryGetValue(spouseId, out int spouse))
                {
                    continue;
                }

                int person = ids[card.Id];
                if (person == spouse)
                {
                    continue;
                }

                desired.Add((Math.Min(person, spouse), Math.Max(person, spouse)));
            }
        }

        var existing = (await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>())).ToList();
        if (replace)
        {
            foreach (var row in existing)
            {
                if (!desired.Contains((row.PersonId, row.SpouseId)))
                {
                    var removed = await people.RemoveSpouseAsync(row.PersonId, row.SpouseId);
                    if (!removed.IsSuccess)
                    {
                        return removed;
                    }
                }
            }
        }

        var have = existing.Select(s => (s.PersonId, s.SpouseId)).ToHashSet();
        foreach (var pair in desired)
        {
            if (have.Contains(pair))
            {
                continue;
            }

            var added = await people.AddSpouseAsync(pair.Low, pair.High);
            if (!added.IsSuccess && added.Status != ResultStatus.Conflict)
            {
                return added;
            }
        }

        return Result.Success();
    }

    private async Task<Result> SyncVitalsAsync(int personId, PersonData card)
    {
        var stored = (await events.GetByPersonAsync(personId)).Value?.ToList() ?? [];
        var birth = await UpsertVitalAsync(personId, stored, PersonEventType.Birth, card.BirthDate, card.BirthPlace);
        if (!birth.IsSuccess)
        {
            return birth;
        }

        return await UpsertVitalAsync(personId, stored, PersonEventType.Death, card.DeathDate, card.DeathPlace);
    }

    private async Task<Result> UpsertVitalAsync(
        int personId,
        IReadOnlyList<PersonEventDto> stored,
        string eventType,
        PartialDate? date,
        string? place)
    {
        var current = stored.FirstOrDefault(e => e.EventType == eventType);
        bool empty = date is null && string.IsNullOrWhiteSpace(place);
        if (empty)
        {
            return current is null ? Result.Success() : await events.DeleteAsync(current.Id);
        }

        var request = new PersonEventRequest
        {
            EventType = eventType,
            Year = (short?)date?.Year,
            Month = (byte?)date?.Month,
            Day = (byte?)date?.Day,
            Place = string.IsNullOrWhiteSpace(place) ? null : place.Trim(),
        };

        return current is null
            ? Plain(await events.CreateAsync(personId, request))
            : Plain(await events.UpdateAsync(current.Id, request));
    }

    private static PersonEditRequest ToRequest(
        PersonData card,
        Person? existing,
        IReadOnlyList<PersonData> cards,
        IReadOnlyDictionary<string, int> ids)
    {
        (int? father, int? mother) = ParentsOf(card, cards, ids);
        return new PersonEditRequest
        {
            FamilyName = card.LastName.Trim(),
            GivenNames = string.IsNullOrWhiteSpace(card.FirstName) ? "Unknown" : card.FirstName.Trim(),
            IsMale = card.Gender == Gender.Unknown ? existing?.IsMale ?? true : card.Gender == Gender.Male,
            FatherId = father,
            MotherId = mother,
            Note = string.IsNullOrWhiteSpace(card.Notes) ? null : card.Notes.Trim(),
            IsDeceased = card.DeathDate is not null,
        };
    }

    private static (int? Father, int? Mother) ParentsOf(
        PersonData card,
        IReadOnlyList<PersonData> cards,
        IReadOnlyDictionary<string, int> ids)
    {
        int? father = null;
        int? mother = null;
        foreach (string parentId in card.Parents)
        {
            if (!ids.TryGetValue(parentId, out int parent))
            {
                continue;
            }

            var source = cards.FirstOrDefault(p => p.Id == parentId);
            if (source?.Gender == Gender.Female)
            {
                mother ??= parent;
            }
            else if (source?.Gender == Gender.Male)
            {
                father ??= parent;
            }
            else if (father is null)
            {
                father = parent;
            }
            else
            {
                mother ??= parent;
            }
        }

        return (father, mother);
    }

    private static PartialDate? ToPartial(PersonEvent? vital)
    {
        if (vital?.Year is not short year)
        {
            return null;
        }

        return PartialDate.TryCreate(year, vital.Month, vital.Day, out var date) ? date : null;
    }

    private static Result Plain<T>(Result<T> result) =>
        result.IsSuccess ? Result.Success() : Result.Error(string.Join(" ", result.Errors));

    private static string? AvatarUrl(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.StartsWith('/') || path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : "/uploads/" + path.TrimStart('/');

    private static string? ImagePath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        const string prefix = "/uploads/";
        return url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? url[prefix.Length..] : url;
    }
}

public sealed record FamilyGraphSnapshot(IReadOnlyList<PersonData> People, string? FocusPersonId);
