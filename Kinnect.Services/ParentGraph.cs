namespace Kinnect.Services;

/// <summary>Who is looking. Visibility of a set of parents is decided against this and nothing else.</summary>
/// <param name="UserId">The signed-in account, or null for code that runs with nobody signed in.</param>
/// <param name="IsAdmin">True for a family administrator.</param>
/// <param name="IsEditor">True for an editor or an administrator.</param>
public sealed record ParentViewer(string? UserId, bool IsAdmin, bool IsEditor)
{
    /// <summary>Nobody signed in, such as a background job. It sees only what everyone sees.</summary>
    public static ParentViewer Nobody { get; } = new(null, false, false);
}

/// <summary>A parent in a set, and the place they hold in it.</summary>
public sealed record ParentMemberView(int PersonId, ParentRole Role);

/// <summary>One set of parents, after the viewer's visibility has been applied.</summary>
public sealed record ParentSetView(
    int Id,
    int ChildId,
    ParentageType Type,
    bool IsPrimary,
    short? Year,
    byte? Month,
    byte? Day,
    LinkVisibility Visibility,
    IReadOnlyList<ParentMemberView> Members)
{
    /// <summary>The parents, father first.</summary>
    public IEnumerable<int> ParentIds => Members.OrderBy(member => member.Role).Select(member => member.PersonId);

    public int? FatherId => Members.FirstOrDefault(member => member.Role == ParentRole.Father)?.PersonId;

    public int? MotherId => Members.FirstOrDefault(member => member.Role == ParentRole.Mother)?.PersonId;
}

/// <summary>
/// Who is whose parent, as one viewer may see it. Hidden sets are never in here, so every feature built on it
/// (relatives, siblings, the tree, a search, an export) is private without having to remember to be.
/// <para>
/// Every feature that follows parents has to pick a <see cref="ParentLineage"/>. The default is
/// <see cref="ParentLineage.Primary"/>: the family the tree draws. <see cref="ParentLineage.BirthOnly"/> is the
/// "blood relatives only" filter.
/// </para>
/// </summary>
public sealed class ParentGraph
{
    private readonly Dictionary<int, List<ParentSetView>> byChild;
    private readonly Dictionary<int, HashSet<int>> childrenByParent = [];

    private ParentGraph(Dictionary<int, List<ParentSetView>> byChild)
    {
        this.byChild = byChild;
        foreach (var set in byChild.Values.SelectMany(sets => sets))
        {
            foreach (var member in set.Members)
            {
                if (!childrenByParent.TryGetValue(member.PersonId, out var children))
                {
                    childrenByParent[member.PersonId] = children = [];
                }

                children.Add(set.ChildId);
            }
        }
    }

    public static ParentGraph Empty { get; } = new([]);

    /// <summary>
    /// Builds the graph from sets that are already filtered for the viewer. When the stored primary set is not in
    /// them (it was hidden), the visible sets are ordered by the default rule and the first one is presented as the
    /// primary one. Stored data is not changed.
    /// </summary>
    public static ParentGraph Build(IEnumerable<ParentSetView> visible)
    {
        var byChild = new Dictionary<int, List<ParentSetView>>();
        foreach (var group in visible.Where(set => set.Members.Count > 0).GroupBy(set => set.ChildId))
        {
            var sets = group.OrderBy(set => set.Id).ToList();
            var primary = sets.FirstOrDefault(set => set.IsPrimary) ?? ChoosePrimary(sets);
            byChild[group.Key] = sets
                .Select(set => set with { IsPrimary = set.Id == primary.Id })
                .OrderByDescending(set => set.IsPrimary)
                .ThenBy(set => set.Id)
                .ToList();
        }

        return new ParentGraph(byChild);
    }

    /// <summary>
    /// Adopted first, then birth, then foster. Ties keep the order the sets were recorded in. This is the rule the
    /// family tree uses too, so the two never disagree about whose family is drawn.
    /// </summary>
    public static ParentSetView ChoosePrimary(IReadOnlyList<ParentSetView> sets) =>
        sets.OrderBy(set => DefaultRank(set.Type)).ThenBy(set => set.Id).First();

    public static int DefaultRank(ParentageType type) => type switch
    {
        ParentageType.Adopted => 0,
        ParentageType.Birth => 1,
        _ => 2,
    };

    /// <summary>Every child who has a set the viewer can see.</summary>
    public IEnumerable<int> Children => byChild.Keys;

    /// <summary>Every visible set, primary sets first within each child.</summary>
    public IEnumerable<ParentSetView> AllSets => byChild.Values.SelectMany(sets => sets);

    public IReadOnlyList<ParentSetView> SetsOf(int childId) => byChild.TryGetValue(childId, out var sets) ? sets : [];

    public ParentSetView? PrimaryOf(int childId) => SetsOf(childId).FirstOrDefault(set => set.IsPrimary);

    /// <summary>The sets a feature should follow for one child.</summary>
    public IReadOnlyList<ParentSetView> Followed(int childId, ParentLineage lineage = ParentLineage.Primary)
    {
        var sets = SetsOf(childId);
        return lineage switch
        {
            ParentLineage.Any => sets,
            ParentLineage.BirthOnly => sets.Where(set => set.Type == ParentageType.Birth).ToList(),
            _ => sets.Where(set => set.IsPrimary).ToList(),
        };
    }

    public IReadOnlyList<int> ParentsOf(int childId, ParentLineage lineage = ParentLineage.Primary) =>
        Followed(childId, lineage).SelectMany(set => set.ParentIds).Distinct().ToList();

    public IReadOnlyList<int> ChildrenOf(int parentId, ParentLineage lineage = ParentLineage.Primary) =>
        childrenByParent.TryGetValue(parentId, out var children)
            ? children.Where(child => Followed(child, lineage).Any(set => set.Members.Any(member => member.PersonId == parentId))).Order().ToList()
            : [];

    /// <summary>People who share a parent with this person, counting only the sets the lineage follows.</summary>
    public IReadOnlyList<int> SiblingsOf(int personId, ParentLineage lineage = ParentLineage.Primary) =>
        ParentsOf(personId, lineage)
            .SelectMany(parent => ChildrenOf(parent, lineage))
            .Where(other => other != personId)
            .Distinct()
            .Order()
            .ToList();

    /// <summary>How the person's parents are recorded, as "Adoptive father" or similar, for one parent of one child.</summary>
    public string? ParentLabel(int childId, int parentId, ParentLineage lineage = ParentLineage.Primary)
    {
        var set = Followed(childId, lineage).FirstOrDefault(candidate => candidate.Members.Any(member => member.PersonId == parentId));
        var member = set?.Members.FirstOrDefault(candidate => candidate.PersonId == parentId);
        return set is null || member is null ? null : ParentageTypes.ParentLabel(set.Type, member.Role);
    }
}
