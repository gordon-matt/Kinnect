using MyWebTree;
using MyWebTree.Graph;
using MyWebTree.Models;
using KinnectParentageType = Kinnect.Enums.ParentageType;
using WebParentageType = MyWebTree.ParentageType;

namespace Kinnect.Services;

/// <summary>
/// Loads the Kinnect people graph into <see cref="PersonData"/> and writes one
/// <see cref="FamilyTreeChange"/> back through the person and event services.
/// Birth and death stay as person events, which is how Kinnect already stores them.
/// <para>
/// Parents are sets (<see cref="IParentSetService"/>). The component only ever gets the sets the signed-in viewer may
/// see, so a hidden link cannot reach it, and a change to a set the viewer cannot see cannot come back from it.
/// </para>
/// </summary>
public sealed class KinnectFamilyGraph(
    IPersonService people,
    IPersonEventService events,
    IParentSetService parentSets,
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
        var lineage = await parentSets.LoadGraphAsync();

        var byId = stored.ToDictionary(p => p.Id);
        var cards = new List<PersonData>(stored.Count);
        foreach (var person in stored)
        {
            var birth = vitals.FirstOrDefault(e => e.PersonId == person.Id && e.EventType == PersonEventType.Birth);
            var death = vitals.FirstOrDefault(e => e.PersonId == person.Id && e.EventType == PersonEventType.Death);
            string id = person.Id.ToString();

            var sets = lineage.SetsOf(person.Id)
                .Select(set => new ParentSetData
                {
                    Id = set.Id.ToString(),
                    Type = ToWeb(set.Type),
                    IsPrimary = set.IsPrimary,
                    ParentIds = set.ParentIds.Where(byId.ContainsKey).Select(parent => parent.ToString()).ToList(),
                    Date = set.Year is short year && PartialDate.TryCreate(year, set.Month, set.Day, out var placed) ? placed : null,
                })
                .Where(set => set.ParentIds.Count > 0)
                .ToList();

            var spouseIds = spouses
                .Where(s => s.PersonId == person.Id || s.SpouseId == person.Id)
                .Select(s => s.PersonId == person.Id ? s.SpouseId : s.PersonId)
                .Where(byId.ContainsKey)
                .Select(s => s.ToString())
                .Distinct()
                .ToList();

            cards.Add(new PersonData
            {
                Id = id,
                Gender = person.IsMale switch { true => Gender.Male, false => Gender.Female, _ => Gender.Unknown },
                FirstName = person.GivenNames,
                LastName = person.FamilyName,
                BirthDate = ToPartial(birth),
                DeathDate = ToPartial(death),
                BirthPlace = birth?.Place,
                DeathPlace = death?.Place,
                AvatarUrl = AvatarUrl(person.ProfileImagePath),
                Notes = person.Note,
                ParentSets = sets,
                Parents = sets.FirstOrDefault(set => set.IsPrimary)?.ParentIds ?? [],
                Spouses = spouseIds,
                Children = lineage.ChildrenOf(person.Id, ParentLineage.Any).Where(byId.ContainsKey).Select(child => child.ToString()).ToList(),
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
            FamilyTreeChangeKind.Linked when change.Link == LinkKind.Spouse => await LinkSpouseAsync(change),
            FamilyTreeChangeKind.Linked or FamilyTreeChangeKind.ParentSetAdded or FamilyTreeChangeKind.ParentSetUpdated or FamilyTreeChangeKind.ParentSetRemoved
                => await SyncAroundAsync(change),
            FamilyTreeChangeKind.Updated => await UpdateOneAsync(change, userId, isAdmin, isEditor),
            FamilyTreeChangeKind.Added => await AddOneAsync(change, userId, isAdmin, isEditor),
            _ => await ReconcileAsync(change, userId, isAdmin, isEditor),
        };
    }

    private async Task<Result> RemoveAsync(string? personId, string userId) => !int.TryParse(personId, out int id) ? Result.Success() : await people.DeleteAsync(id, userId);

    private async Task<Result> LinkSpouseAsync(FamilyTreeChange change)
    {
        if (!int.TryParse(change.PersonId, out int left) || !int.TryParse(change.OtherPersonId, out int right))
        {
            return Result.Invalid(new ValidationError("Those people are not in Kinnect yet."));
        }

        var added = await people.AddSpouseAsync(left, right);
        return added.IsSuccess || added.Status == ResultStatus.Conflict ? Result.Success() : added;
    }

    private async Task<Result> UnlinkAsync(FamilyTreeChange change)
    {
        if (!int.TryParse(change.PersonId, out int left) || !int.TryParse(change.OtherPersonId, out int right))
        {
            return Result.Invalid(new ValidationError("Those people are not in Kinnect yet."));
        }

        return change.Link == LinkKind.Spouse
            ? await people.RemoveSpouseAsync(left, right)
            : await SyncAroundAsync(change);
    }

    /// <summary>
    /// A link, an unlink or a change to a set touches the sets of the child. Whichever of the two people is the child
    /// (or both, when it is not clear) is brought in line with what the component sent.
    /// </summary>
    private async Task<Result> SyncAroundAsync(FamilyTreeChange change)
    {
        var map = change.People
            .Where(person => int.TryParse(person.Id, out _))
            .ToDictionary(person => person.Id, person => int.Parse(person.Id));
        foreach (string? id in new[] { change.PersonId, change.OtherPersonId }.Where(id => id is not null).Distinct())
        {
            var card = change.People.FirstOrDefault(person => person.Id == id);
            if (card is null || !map.ContainsKey(card.Id))
            {
                continue;
            }

            var result = await SyncSetsAsync(card, map);
            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return Result.Success();
    }

    /// <summary>Brings the sets the viewer can see for one child in line with the card. Sets with a parent who is not saved yet are skipped.</summary>
    private async Task<Result> SyncSetsAsync(PersonData card, IReadOnlyDictionary<string, int> ids)
    {
        var desired = new List<ParentSetSync>();
        foreach (var set in ParentSetRules.SetsOf(card))
        {
            var parents = set.ParentIds.Where(ids.ContainsKey).Select(parent => ids[parent]).Distinct().ToList();
            if (parents.Count == 0)
            {
                continue;
            }

            desired.Add(new ParentSetSync(
                set.Id,
                ToKinnect(set.Type),
                parents,
                (short?)set.Date?.Year,
                (byte?)set.Date?.Month,
                (byte?)set.Date?.Day,
                set.IsPrimary));
        }

        return await parentSets.SyncAsync(ids[card.Id], desired);
    }

    private static WebParentageType ToWeb(KinnectParentageType type) => type switch
    {
        KinnectParentageType.Adopted => WebParentageType.Adopted,
        KinnectParentageType.Foster => WebParentageType.Foster,
        _ => WebParentageType.Birth,
    };

    private static KinnectParentageType ToKinnect(WebParentageType type) => type switch
    {
        WebParentageType.Adopted => KinnectParentageType.Adopted,
        WebParentageType.Foster => KinnectParentageType.Foster,
        _ => KinnectParentageType.Birth,
    };
    private async Task<Result> UpdateOneAsync(FamilyTreeChange change, string userId, bool isAdmin, bool isEditor)
    {
        var card = change.People.FirstOrDefault(p => p.Id == change.PersonId);
        if (card is null || !int.TryParse(card.Id, out int id))
        {
            return Result.NotFound("Person not found.");
        }

        var result = await SaveFieldsAsync(card, id, userId, isAdmin, isEditor);
        return !result.IsSuccess ? result : await SyncVitalsAsync(id, card);
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

        var created = await people.CreateAsync(ToRequest(card, null));
        if (!created.IsSuccess)
        {
            return Plain(created);
        }

        map[card.Id] = created.Value.Id;
        var linked = await SaveFieldsAsync(card, created.Value.Id, userId, isAdmin, isEditor);
        if (!linked.IsSuccess)
        {
            return linked;
        }

        await SyncVitalsAsync(created.Value.Id, card);
        await SyncSpousesAsync(change.People, map, replace: false);

        // The new person can be a child (their own sets) and a parent (the sets of the people listed as their children).
        foreach (var affected in change.People.Where(person => person.Id == card.Id || card.Children.Contains(person.Id)))
        {
            if (!map.ContainsKey(affected.Id))
            {
                continue;
            }

            var synced = await SyncSetsAsync(affected, map);
            if (!synced.IsSuccess)
            {
                return synced;
            }
        }

        return Result.Success();
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

            var created = await people.CreateAsync(ToRequest(card, null));
            if (!created.IsSuccess)
            {
                return Plain(created);
            }

            map[card.Id] = created.Value.Id;
        }

        foreach (var card in change.People.Where(p => !p.IsPlaceholder))
        {
            var saved = await SaveFieldsAsync(card, map[card.Id], userId, isAdmin, isEditor);
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

        var parentResult = await SyncAllSetsAsync(change.People, map);
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
        bool isEditor)
    {
        var existing = await personRepository.FindOneAsync(id);
        if (existing is null)
        {
            return Result.NotFound("Person not found.");
        }

        string? image = ImagePath(card.AvatarUrl) ?? existing.ProfileImagePath;
        var request = new PersonEditRequest
        {
            FamilyName = card.LastName.Trim(),
            GivenNames = string.IsNullOrWhiteSpace(card.FirstName) ? existing.GivenNames : card.FirstName.Trim(),
            IsMale = card.Gender switch { Gender.Male => true, Gender.Female => false, _ => existing.IsMale },
            Latitude = existing.Latitude,
            Longitude = existing.Longitude,
            Occupation = existing.Occupation,
            Education = existing.Education,
            Religion = existing.Religion,
            Note = string.IsNullOrWhiteSpace(card.Notes) ? null : card.Notes.Trim(),
            IsDeceased = card.DeathDate is not null,
        };

        var updated = await people.UpdateAsync(id, request, userId, isAdmin, isEditor);
        return !updated.IsSuccess
            ? Plain(updated)
            : image != existing.ProfileImagePath && image is not null
            ? await people.UpdateProfileImageAsync(id, image, userId, isAdmin, isEditor)
            : Result.Success();
    }

    /// <summary>
    /// Undo, redo and other whole-graph replaces. A person the signed-in user may not edit is skipped rather than
    /// failing the rest, as before.
    /// </summary>
    private async Task<Result> SyncAllSetsAsync(IReadOnlyList<PersonData> cards, IReadOnlyDictionary<string, int> ids)
    {
        foreach (var card in cards.Where(p => !p.IsPlaceholder && ids.ContainsKey(p.Id)))
        {
            var result = await SyncSetsAsync(card, ids);
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
        return !birth.IsSuccess ? birth : await UpsertVitalAsync(personId, stored, PersonEventType.Death, card.DeathDate, card.DeathPlace);
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
        Person? existing)
    {
        return new PersonEditRequest
        {
            FamilyName = card.LastName.Trim(),
            GivenNames = string.IsNullOrWhiteSpace(card.FirstName) ? "Unknown" : card.FirstName.Trim(),
            IsMale = card.Gender switch { Gender.Male => true, Gender.Female => false, _ => existing?.IsMale },
            Note = string.IsNullOrWhiteSpace(card.Notes) ? null : card.Notes.Trim(),
            IsDeceased = card.DeathDate is not null,
        };
    }

    private static PartialDate? ToPartial(PersonEvent? vital) => vital?.Year is not short year ? null : PartialDate.TryCreate(year, vital.Month, vital.Day, out var date) ? date : null;

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