using MyWebTree.Models;

namespace Kinnect.Services;

/// <summary>Who may see and change a set of parents.</summary>
public static class ParentSetAccess
{
    /// <summary>
    /// Everyone in the family sees <see cref="LinkVisibility.Everyone"/> sets. Administrators see
    /// <see cref="LinkVisibility.FamilyAdmins"/> ones. <see cref="LinkVisibility.ChildOnly"/> belongs to the child's
    /// own account and nobody else, administrators included.
    /// </summary>
    public static bool CanSee(LinkVisibility visibility, ParentViewer viewer, string? childUserId) => visibility switch
    {
        LinkVisibility.Everyone => true,
        LinkVisibility.FamilyAdmins => viewer.IsAdmin,
        LinkVisibility.ChildOnly => viewer.UserId is not null && string.Equals(viewer.UserId, childUserId, StringComparison.Ordinal),
        _ => false,
    };

    /// <summary>The same rule as editing the person: themselves, an administrator, or an editor for someone with no account.</summary>
    public static bool CanEditChild(Person child, ParentViewer viewer) =>
        viewer.UserId is not null
        && (viewer.IsAdmin || child.UserId == viewer.UserId || (viewer.IsEditor && child.UserId is null));

    /// <summary>Only a family administrator or the child can change who may see a set.</summary>
    public static bool CanChangeVisibility(Person child, ParentViewer viewer) =>
        viewer.UserId is not null && (viewer.IsAdmin || child.UserId == viewer.UserId);
}

public class ParentSetService(
    IRepository<ParentSet> setRepository,
    IRepository<ParentSetMember> memberRepository,
    IRepository<Person> personRepository,
    IRepository<PersonEvent> eventRepository,
    IUserContextService userContext) : IParentSetService
{
    private const string Hidden = "Those parents can't be added.";

    public ParentViewer Viewer
    {
        get
        {
            string? userId = userContext.GetCurrentUserId();
            return userId is null ? ParentViewer.Nobody : new ParentViewer(userId, userContext.IsAdmin(), userContext.IsEditor());
        }
    }

    public async Task<ParentGraph> LoadGraphAsync(bool publicOnly = false)
    {
        var data = await LoadAsync();
        return ParentGraph.Build(data.VisibleViews(Viewer, publicOnly));
    }

    public async Task<IReadOnlyList<ParentSetDto>> GetForChildAsync(int childId)
    {
        var data = await LoadAsync();
        return ParentGraph.Build(data.VisibleViews(Viewer, publicOnly: false).Where(set => set.ChildId == childId))
            .SetsOf(childId)
            .Select(ToDto)
            .ToList();
    }

    public async Task<Result<ParentSetDto>> AddAsync(int childId, ParentSetRequest request)
    {
        var data = await LoadAsync();
        if (!data.People.TryGetValue(childId, out var child))
        {
            return Result.NotFound("Person not found.");
        }

        var viewer = Viewer;
        if (!ParentSetAccess.CanEditChild(child, viewer))
        {
            return Result.Forbidden();
        }

        var parentIds = ParentIdsOf(request);
        var problem = CheckRequest(data, child, parentIds, request, replacing: null);
        if (problem is not null)
        {
            return problem.Value.Status == ResultStatus.Conflict ? Result.Conflict(problem.Value.Message) : Result.Invalid(new ValidationError(problem.Value.Message));
        }

        if (request.Visibility != LinkVisibility.Everyone && !ParentSetAccess.CanChangeVisibility(child, viewer))
        {
            return Result.Forbidden("Only an administrator or the person themselves can limit who sees these parents.");
        }

        var existing = data.SetsOf(childId).ToList();
        bool primary = existing.Count == 0 || request.MakePrimary;
        if (primary)
        {
            await DemoteAsync(existing, except: null);
        }

        var set = await setRepository.InsertAsync(new ParentSet
        {
            ChildPersonId = childId,
            Type = request.Type,
            IsPrimary = primary,
            Year = request.Type == ParentageType.Birth ? null : request.Year,
            Month = request.Type == ParentageType.Birth || request.Year is null ? null : request.Month,
            Day = request.Type == ParentageType.Birth || request.Year is null || request.Month is null ? null : request.Day,
            Visibility = request.Visibility,
            CreatedAtUtc = DateTime.UtcNow,
        });

        var members = await InsertMembersAsync(set.Id, parentIds, data.People, existingRoles: null);
        return Result.Success(ToDto(set, members));
    }

    public async Task<Result<ParentSetDto>> UpdateAsync(int setId, ParentSetRequest request)
    {
        var data = await LoadAsync();
        var viewer = Viewer;
        var set = data.Sets.FirstOrDefault(candidate => candidate.Id == setId);
        if (set is null || !data.People.TryGetValue(set.ChildPersonId, out var child) || !ParentSetAccess.CanSee(set.Visibility, viewer, child.UserId))
        {
            return Result.NotFound("Those parents are no longer there.");
        }

        if (!ParentSetAccess.CanEditChild(child, viewer))
        {
            return Result.Forbidden();
        }

        var parentIds = ParentIdsOf(request);
        var problem = CheckRequest(data, child, parentIds, request, replacing: set);
        if (problem is not null)
        {
            return problem.Value.Status == ResultStatus.Conflict ? Result.Conflict(problem.Value.Message) : Result.Invalid(new ValidationError(problem.Value.Message));
        }

        if (request.Visibility != set.Visibility)
        {
            if (!ParentSetAccess.CanChangeVisibility(child, viewer))
            {
                return Result.Forbidden("Only an administrator or the person themselves can limit who sees these parents.");
            }

            set.Visibility = request.Visibility;
        }

        var existingRoles = data.MembersOf(set.Id).ToDictionary(member => member.ParentPersonId, member => member.Role);
        set.Type = request.Type;
        set.Year = request.Type == ParentageType.Birth ? null : request.Year;
        set.Month = request.Type == ParentageType.Birth || request.Year is null ? null : request.Month;
        set.Day = request.Type == ParentageType.Birth || request.Year is null || request.Month is null ? null : request.Day;
        if (request.MakePrimary && !set.IsPrimary)
        {
            await DemoteAsync(data.SetsOf(set.ChildPersonId), except: set.Id);
            set.IsPrimary = true;
        }

        await setRepository.UpdateAsync(set);
        await memberRepository.DeleteAsync(member => member.ParentSetId == set.Id);
        var members = await InsertMembersAsync(set.Id, parentIds, data.People, existingRoles);
        return Result.Success(ToDto(set, members));
    }

    public async Task<Result> RemoveAsync(int setId)
    {
        var data = await LoadAsync();
        var set = data.Sets.FirstOrDefault(candidate => candidate.Id == setId);
        if (set is null || !data.People.TryGetValue(set.ChildPersonId, out var child) || !ParentSetAccess.CanSee(set.Visibility, Viewer, child.UserId))
        {
            return Result.NotFound("Those parents are no longer there.");
        }

        if (!ParentSetAccess.CanEditChild(child, Viewer))
        {
            return Result.Forbidden();
        }

        await DeleteSetAsync(set);
        await EnsurePrimaryAsync(set.ChildPersonId);
        return Result.Success();
    }

    public async Task<Result> AddParentAsync(int setId, int parentId)
    {
        var data = await LoadAsync();
        var set = data.Sets.FirstOrDefault(candidate => candidate.Id == setId);
        if (set is null)
        {
            return Result.NotFound("Those parents are no longer there.");
        }

        var current = data.MembersOf(setId).Select(member => member.ParentPersonId).ToList();
        if (current.Contains(parentId))
        {
            return Result.Success();
        }

        return await ReplaceMembersAsync(set, [.. current, parentId]);
    }

    public async Task<Result> RemoveParentAsync(int setId, int parentId)
    {
        var data = await LoadAsync();
        var set = data.Sets.FirstOrDefault(candidate => candidate.Id == setId);
        if (set is null)
        {
            return Result.NotFound("Those parents are no longer there.");
        }

        var remaining = data.MembersOf(setId).Select(member => member.ParentPersonId).Where(id => id != parentId).ToList();
        return remaining.Count == 0 ? await RemoveAsync(setId) : await ReplaceMembersAsync(set, remaining);
    }

    public async Task<Result> SetBirthParentsAsync(int childId, int? fatherId, int? motherId)
    {
        var data = await LoadAsync();
        var viewer = Viewer;
        if (!data.People.TryGetValue(childId, out var child))
        {
            return Result.NotFound("Person not found.");
        }

        var visible = ParentGraph.Build(data.VisibleViews(viewer, publicOnly: false)).SetsOf(childId)
            .Where(set => set.Type == ParentageType.Birth)
            .ToList();
        var current = visible.FirstOrDefault();
        if (fatherId is null && motherId is null)
        {
            return current is null ? Result.Success() : await RemoveAsync(current.Id);
        }

        var request = new ParentSetRequest
        {
            Type = ParentageType.Birth,
            FatherId = fatherId,
            MotherId = motherId,
            Visibility = current?.Visibility ?? LinkVisibility.Everyone,
            MakePrimary = current?.IsPrimary ?? false,
        };

        var result = current is null ? await AddAsync(childId, request) : await UpdateAsync(current.Id, request);
        return result.IsSuccess ? Result.Success() : result.Status == ResultStatus.Invalid ? Result.Invalid(result.ValidationErrors) : Result.Error(string.Join(" ", result.Errors));
    }

    public async Task<Result> SyncAsync(int childId, IReadOnlyList<ParentSetSync> desired)
    {
        var data = await LoadAsync();
        var viewer = Viewer;
        if (!data.People.TryGetValue(childId, out var child))
        {
            return Result.NotFound("Person not found.");
        }

        var visibleStored = data.SetsOf(childId).Where(set => ParentSetAccess.CanSee(set.Visibility, viewer, child.UserId)).ToList();
        var projected = ParentGraph.Build(data.VisibleViews(viewer, publicOnly: false).Where(set => set.ChildId == childId)).PrimaryOf(childId);

        var matched = new Dictionary<int, ParentSetSync>();
        var added = new List<ParentSetSync>();
        foreach (var wanted in desired.Where(set => set.ParentIds.Count > 0))
        {
            if (int.TryParse(wanted.Id, out int id) && visibleStored.Any(set => set.Id == id) && !matched.ContainsKey(id))
            {
                matched[id] = wanted;
            }
            else
            {
                added.Add(wanted);
            }
        }

        var wantedPrimary = desired.FirstOrDefault(set => set.IsPrimary);
        int? primaryId = wantedPrimary is not null && int.TryParse(wantedPrimary.Id, out int parsed) && matched.ContainsKey(parsed) ? parsed : null;
        bool primaryChanged = primaryId is not null && projected is not null && primaryId != projected.Id;

        foreach (var set in visibleStored.Where(set => !matched.ContainsKey(set.Id)))
        {
            var removed = await RemoveAsync(set.Id);
            if (!removed.IsSuccess)
            {
                return removed;
            }
        }

        foreach ((int id, var wanted) in matched)
        {
            var set = visibleStored.First(candidate => candidate.Id == id);
            var members = data.MembersOf(id).Select(member => member.ParentPersonId).Order().ToList();
            bool same = set.Type == wanted.Type
                && members.SequenceEqual(wanted.ParentIds.Order())
                && set.Year == wanted.Year && set.Month == wanted.Month && set.Day == wanted.Day;
            bool promote = primaryChanged && primaryId == id;
            if (same && !promote)
            {
                continue;
            }

            var (fatherId, motherId) = RolesFor(wanted.ParentIds, data.People, data.MembersOf(id).ToDictionary(member => member.ParentPersonId, member => member.Role));
            var updated = await UpdateAsync(id, new ParentSetRequest
            {
                Type = wanted.Type,
                FatherId = fatherId,
                MotherId = motherId,
                Year = wanted.Year,
                Month = wanted.Month,
                Day = wanted.Day,
                Visibility = set.Visibility,
                MakePrimary = promote,
            });
            if (!updated.IsSuccess)
            {
                return PlainFailure(updated);
            }
        }

        foreach (var wanted in added)
        {
            var (newFather, newMother) = RolesFor(wanted.ParentIds, data.People, null);
            var created = await AddAsync(childId, new ParentSetRequest
            {
                Type = wanted.Type,
                FatherId = newFather,
                MotherId = newMother,
                Year = wanted.Year,
                Month = wanted.Month,
                Day = wanted.Day,
                Visibility = LinkVisibility.Everyone,
                // The first set a child ever gets is primary anyway. When the viewer sees no set because the
                // stored ones are hidden, a new set must not take the primary place from a set they cannot see.
                MakePrimary = projected is not null && ReferenceEquals(wanted, wantedPrimary),
            });
            if (!created.IsSuccess)
            {
                return PlainFailure(created);
            }
        }

        await EnsurePrimaryAsync(childId);
        return Result.Success();
    }

    public async Task DetachParentAsync(int personId)
    {
        var data = await LoadAsync();
        var memberships = data.Members.Where(member => member.ParentPersonId == personId).ToList();
        foreach (var member in memberships)
        {
            await memberRepository.DeleteAsync(member);
        }

        foreach (int setId in memberships.Select(member => member.ParentSetId).Distinct())
        {
            var set = data.Sets.First(candidate => candidate.Id == setId);
            if (!data.MembersOf(setId).Any(member => member.ParentPersonId != personId))
            {
                await setRepository.DeleteAsync(set);
            }

            await EnsurePrimaryAsync(set.ChildPersonId);
        }
    }

    private async Task<Result> ReplaceMembersAsync(ParentSet set, IReadOnlyList<int> parentIds)
    {
        var (fatherId, motherId) = RolesFor(parentIds, (await LoadAsync()).People, null);
        var result = await UpdateAsync(set.Id, new ParentSetRequest
        {
            Type = set.Type,
            FatherId = fatherId,
            MotherId = motherId,
            Year = set.Year,
            Month = set.Month,
            Day = set.Day,
            Visibility = set.Visibility,
        });
        return result.IsSuccess ? Result.Success() : PlainFailure(result);
    }

    private (string Message, ResultStatus Status)? CheckRequest(Stored data, Person child, IReadOnlyList<int> parentIds, ParentSetRequest request, ParentSet? replacing)
    {
        if (!Enum.IsDefined(request.Type) || !Enum.IsDefined(request.Visibility))
        {
            return ("Choose birth, adopted or foster.", ResultStatus.Invalid);
        }

        if (parentIds.Count == 0)
        {
            return ("A set of parents needs a father, a mother, or both.", ResultStatus.Invalid);
        }

        if (parentIds.Any(id => !data.People.ContainsKey(id)))
        {
            return ("That person is no longer in the family.", ResultStatus.Invalid);
        }

        PartialDate? date = null;
        if (request.Type != ParentageType.Birth && request.Year is short year)
        {
            if (!PartialDate.TryCreate(year, request.Month, request.Month is null ? null : request.Day, out var parsed))
            {
                return ("Use a real date, such as 1987, 1987-06 or 1987-06-14.", ResultStatus.Invalid);
            }

            date = parsed;
        }

        var validator = data.Validator();
        validator.Clear(child.Id);
        foreach (var other in data.SetsOf(child.Id).Where(set => set.Id != replacing?.Id))
        {
            validator.Add(child.Id, data.MembersOf(other.Id).Select(member => member.ParentPersonId));
        }

        string? problem = validator.Check(child.Id, parentIds, date);
        if (problem is not null)
        {
            return (problem, ResultStatus.Invalid);
        }

        var viewer = Viewer;
        if (request.Type == ParentageType.Birth)
        {
            var otherBirth = data.SetsOf(child.Id).FirstOrDefault(set => set.Id != replacing?.Id && set.Type == ParentageType.Birth);
            if (otherBirth is not null)
            {
                // A hidden birth set is refused with the same words as any other hidden clash, so its existence stays private.
                return ParentSetAccess.CanSee(otherBirth.Visibility, viewer, child.UserId)
                    ? ($"{child.GivenNames} already has birth parents.", ResultStatus.Conflict)
                    : (Hidden, ResultStatus.Conflict);
            }
        }

        var duplicate = data.SetsOf(child.Id).FirstOrDefault(set =>
            set.Id != replacing?.Id
            && set.Type == request.Type
            && data.MembersOf(set.Id).Select(member => member.ParentPersonId).Order().SequenceEqual(parentIds.Order()));
        return duplicate is null
            ? null
            : ParentSetAccess.CanSee(duplicate.Visibility, viewer, child.UserId)
                ? ($"{child.GivenNames} already has that set of parents.", ResultStatus.Conflict)
                : (Hidden, ResultStatus.Conflict);
    }

    private static List<int> ParentIdsOf(ParentSetRequest request) =>
        new[] { request.FatherId, request.MotherId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

    private async Task<List<ParentSetMember>> InsertMembersAsync(
        int setId,
        IReadOnlyList<int> parentIds,
        IReadOnlyDictionary<int, Person> people,
        IReadOnlyDictionary<int, ParentRole>? existingRoles)
    {
        var roles = Roles(parentIds, people, existingRoles);
        var members = new List<ParentSetMember>();
        foreach (int parentId in parentIds)
        {
            members.Add(await memberRepository.InsertAsync(new ParentSetMember { ParentSetId = setId, ParentPersonId = parentId, Role = roles[parentId] }));
        }

        return members;
    }

    private static (int? FatherId, int? MotherId) RolesFor(IReadOnlyList<int> parentIds, IReadOnlyDictionary<int, Person> people, IReadOnlyDictionary<int, ParentRole>? existing)
    {
        var roles = Roles(parentIds, people, existing);
        int? father = parentIds.Where(id => roles[id] == ParentRole.Father).Select(id => (int?)id).FirstOrDefault();
        int? mother = parentIds.Where(id => roles[id] == ParentRole.Mother).Select(id => (int?)id).FirstOrDefault();
        return (father, mother);
    }

    /// <summary>
    /// Keeps the place each parent already had. Otherwise a woman is the mother, a man the father, and when that does
    /// not settle it (two fathers, or an unknown sex) the first is the father and the second the mother.
    /// </summary>
    internal static Dictionary<int, ParentRole> Roles(IReadOnlyList<int> parentIds, IReadOnlyDictionary<int, Person> people, IReadOnlyDictionary<int, ParentRole>? existing)
    {
        var roles = new Dictionary<int, ParentRole>();
        foreach (int id in parentIds)
        {
            if (existing is not null && existing.TryGetValue(id, out var kept) && !roles.ContainsValue(kept))
            {
                roles[id] = kept;
            }
        }

        foreach (int id in parentIds.Where(id => !roles.ContainsKey(id)))
        {
            bool? male = people.TryGetValue(id, out var person) ? person.IsMale : null;
            var preferred = male == false ? ParentRole.Mother : ParentRole.Father;
            var role = !roles.ContainsValue(preferred) ? preferred : preferred == ParentRole.Father ? ParentRole.Mother : ParentRole.Father;
            roles[id] = role;
        }

        return roles;
    }

    private async Task DemoteAsync(IEnumerable<ParentSet> sets, int? except)
    {
        foreach (var set in sets.Where(set => set.IsPrimary && set.Id != except))
        {
            set.IsPrimary = false;
            await setRepository.UpdateAsync(set);
        }
    }

    private async Task DeleteSetAsync(ParentSet set)
    {
        await memberRepository.DeleteAsync(member => member.ParentSetId == set.Id);
        await setRepository.DeleteAsync(set);
    }

    /// <summary>A child with sets has exactly one primary one. When it was removed, the default rule picks the next.</summary>
    private async Task EnsurePrimaryAsync(int childId)
    {
        var sets = (await setRepository.FindAsync(new SearchOptions<ParentSet> { Query = set => set.ChildPersonId == childId })).ToList();
        if (sets.Count == 0)
        {
            return;
        }

        var primary = sets.Where(set => set.IsPrimary).OrderBy(set => set.Id).FirstOrDefault()
            ?? sets.OrderBy(set => ParentGraph.DefaultRank(set.Type)).ThenBy(set => set.Id).First();
        foreach (var set in sets.Where(set => set.IsPrimary != (set.Id == primary.Id)))
        {
            set.IsPrimary = set.Id == primary.Id;
            await setRepository.UpdateAsync(set);
        }
    }

    private static Result PlainFailure<T>(Result<T> result) => result.Status switch
    {
        ResultStatus.Invalid => Result.Invalid(result.ValidationErrors),
        ResultStatus.NotFound => Result.NotFound(result.Errors.ToArray()),
        ResultStatus.Forbidden => Result.Forbidden(result.Errors.ToArray()),
        ResultStatus.Conflict => Result.Conflict(result.Errors.ToArray()),
        _ => Result.Error(string.Join(" ", result.Errors)),
    };

    private static ParentSetDto ToDto(ParentSetView set) => new()
    {
        Id = set.Id,
        ChildPersonId = set.ChildId,
        Type = set.Type,
        IsPrimary = set.IsPrimary,
        Year = set.Year,
        Month = set.Month,
        Day = set.Day,
        Visibility = set.Visibility,
        Members = set.Members.OrderBy(member => member.Role).Select(member => new ParentSetMemberDto { PersonId = member.PersonId, Role = member.Role }).ToList(),
    };

    private static ParentSetDto ToDto(ParentSet set, IEnumerable<ParentSetMember> members) => new()
    {
        Id = set.Id,
        ChildPersonId = set.ChildPersonId,
        Type = set.Type,
        IsPrimary = set.IsPrimary,
        Year = set.Year,
        Month = set.Month,
        Day = set.Day,
        Visibility = set.Visibility,
        Members = members.OrderBy(member => member.Role).Select(member => new ParentSetMemberDto { PersonId = member.ParentPersonId, Role = member.Role }).ToList(),
    };

    private async Task<Stored> LoadAsync()
    {
        var sets = (await setRepository.FindAsync(new SearchOptions<ParentSet>())).ToList();
        var members = (await memberRepository.FindAsync(new SearchOptions<ParentSetMember>())).ToList();
        var people = (await personRepository.FindAsync(new SearchOptions<Person>())).ToDictionary(person => person.Id);
        var births = (await eventRepository.FindAsync(new SearchOptions<PersonEvent> { Query = evt => evt.EventType == PersonEventType.Birth })).ToList();
        return new Stored(sets, members, people, births);
    }

    /// <summary>Everything stored about parents, hidden sets included. It never leaves this class.</summary>
    private sealed class Stored(List<ParentSet> sets, List<ParentSetMember> members, Dictionary<int, Person> people, List<PersonEvent> births)
    {
        private readonly ILookup<int, ParentSetMember> membersBySet = members.ToLookup(member => member.ParentSetId);
        private readonly ILookup<int, ParentSet> setsByChild = sets.ToLookup(set => set.ChildPersonId);

        public List<ParentSet> Sets { get; } = sets;

        public List<ParentSetMember> Members { get; } = members;

        public Dictionary<int, Person> People { get; } = people;

        public IEnumerable<ParentSetMember> MembersOf(int setId) => membersBySet[setId];

        public IEnumerable<ParentSet> SetsOf(int childId) => setsByChild[childId];

        public ParentSetValidator Validator() => new(
            Sets.Select(set => (set.ChildPersonId, MembersOf(set.Id).Select(member => member.ParentPersonId))),
            births,
            People.Values);

        public IEnumerable<ParentSetView> VisibleViews(ParentViewer viewer, bool publicOnly) => Sets
            .Where(set => People.TryGetValue(set.ChildPersonId, out var child)
                && (publicOnly ? set.Visibility == LinkVisibility.Everyone : ParentSetAccess.CanSee(set.Visibility, viewer, child.UserId)))
            .Select(set => new ParentSetView(
                set.Id,
                set.ChildPersonId,
                set.Type,
                set.IsPrimary,
                set.Year,
                set.Month,
                set.Day,
                set.Visibility,
                MembersOf(set.Id).Where(member => People.ContainsKey(member.ParentPersonId)).Select(member => new ParentMemberView(member.ParentPersonId, member.Role)).ToList()));
    }
}
