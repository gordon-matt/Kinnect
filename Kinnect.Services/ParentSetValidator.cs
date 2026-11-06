using MyWebTree.Models;

namespace Kinnect.Services;

/// <summary>
/// The rules every set of parents has to pass, whether it comes from the family tree, the API or a GEDCOM import.
/// They run against the whole stored graph, hidden sets included, because a loop can run through a link the viewer
/// cannot see. The messages never say which link was the hidden one.
/// </summary>
public sealed class ParentSetValidator
{
    private readonly Dictionary<int, HashSet<int>> parentsOf = [];
    private readonly Dictionary<int, PartialDate> births = [];
    private readonly Dictionary<int, string> names = [];

    /// <summary>Starts from the sets that are already stored, the birth dates, and the names used in messages.</summary>
    public ParentSetValidator(
        IEnumerable<(int ChildId, IEnumerable<int> ParentIds)> storedSets,
        IEnumerable<PersonEvent> birthEvents,
        IEnumerable<Person> people)
    {
        foreach ((int childId, var parentIds) in storedSets)
        {
            Add(childId, parentIds);
        }

        foreach (var birth in birthEvents.Where(evt => evt.EventType == PersonEventType.Birth && evt.Year is not null))
        {
            if (PartialDate.TryCreate(birth.Year!.Value, birth.Month, birth.Day, out var date)
                && (!births.TryGetValue(birth.PersonId, out var known) || date.IsBefore(known)))
            {
                births[birth.PersonId] = date;
            }
        }

        foreach (var person in people)
        {
            names[person.Id] = $"{person.GivenNames} {person.FamilyName}".Trim();
        }
    }

    /// <summary>Records a set that was accepted, so the sets after it are checked against it.</summary>
    public void Add(int childId, IEnumerable<int> parentIds)
    {
        if (!parentsOf.TryGetValue(childId, out var parents))
        {
            parentsOf[childId] = parents = [];
        }

        foreach (int parentId in parentIds)
        {
            parents.Add(parentId);
        }
    }

    /// <summary>Forgets every set of one child. Use it before checking an edit of one of that child's sets.</summary>
    public void Clear(int childId) => parentsOf.Remove(childId);

    /// <summary>
    /// Checks a set before it is stored. Returns null when it is fine, and otherwise the reason, in words the
    /// person who tried it can act on.
    /// </summary>
    /// <param name="childId">The child.</param>
    /// <param name="parentIds">One or two people.</param>
    /// <param name="date">When the adoption or placement happened, if known.</param>
    public string? Check(int childId, IReadOnlyList<int> parentIds, PartialDate? date = null)
    {
        if (parentIds.Count is < 1 or > 2)
        {
            return "A set of parents needs one or two people.";
        }

        if (parentIds.Distinct().Count() != parentIds.Count)
        {
            return $"{Name(childId)} lists the same parent twice.";
        }

        if (parentIds.Contains(childId))
        {
            return "A person can't be their own parent.";
        }

        foreach (int parentId in parentIds)
        {
            if (IsAncestorOf(childId, parentId))
            {
                return $"That would make {Name(childId)} their own ancestor.";
            }

            if (births.TryGetValue(parentId, out var parentBorn) && births.TryGetValue(childId, out var childBorn) && childBorn.IsBefore(parentBorn))
            {
                return $"{Name(childId)} can't be born before {Name(parentId)}.";
            }
        }

        if (date is PartialDate placed && births.TryGetValue(childId, out var born) && placed.IsBefore(born))
        {
            return $"{Name(childId)}'s parents can't be dated before they were born.";
        }

        return null;
    }

    private bool IsAncestorOf(int possibleAncestor, int person)
    {
        var seen = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(person);
        while (pending.Count > 0)
        {
            int current = pending.Pop();
            if (!seen.Add(current) || !parentsOf.TryGetValue(current, out var parents))
            {
                continue;
            }

            foreach (int parent in parents)
            {
                if (parent == possibleAncestor)
                {
                    return true;
                }

                pending.Push(parent);
            }
        }

        return false;
    }

    private string Name(int personId) => names.TryGetValue(personId, out string? name) && name.Length > 0 ? name : "That person";
}
