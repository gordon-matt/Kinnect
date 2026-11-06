using GedcomSharp.Model;
using Kinnect.Models.FamilyTree;
using MyWebTree.Models;
using GedcomStructure = GedcomSharp.GedcomStructure;
using GedcomTags = GedcomSharp.GedcomTags;

namespace Kinnect.Services;

/// <summary>
/// Adopted and foster parents in GEDCOM 5.5.1 and 7.0.
/// <para>
/// A <c>FAM</c> record is a family unit, so each set of parents is a <c>FAM</c> whose <c>HUSB</c> and <c>WIFE</c> are the
/// members (one or two) and whose <c>CHIL</c> includes the child. The child's <c>FAMC</c> says how, in <c>PEDI</c>.
/// </para>
/// </summary>
public partial class GedcomService
{
    /// <summary>The custom tag written under the primary <c>FAMC</c>. GEDCOM has no primary flag, so other programs ignore it.</summary>
    internal const string PrimaryFamilyTag = "_PRIM";

    /// <summary>One way a person points at a family as a child: a <c>FAMC</c> link, an <c>ADOP</c> event, or both.</summary>
    private sealed class ParentLink(string familyXref, int order)
    {
        public string FamilyXref { get; } = familyXref;

        /// <summary>Position of the <c>FAMC</c> in the record. An <c>ADOP</c> event with no <c>FAMC</c> comes after every <c>FAMC</c>.</summary>
        public int Order { get; } = order;

        public bool HasLink { get; set; }

        /// <summary>The <c>PEDI</c> value, upper case, or null when the link has none.</summary>
        public string? Pedigree { get; set; }

        public bool Primary { get; set; }

        public bool HasAdoption { get; set; }

        /// <summary><c>HUSB</c>, <c>WIFE</c> or <c>BOTH</c> from <c>FAMC</c> &gt; <c>ADOP</c> under the event.</summary>
        public string? Adopter { get; set; }

        public (short? Year, byte? Month, byte? Day) Date { get; set; }
    }

    private sealed record ImportedSet(
        ParentLink Link,
        ParentageType Type,
        List<(int PersonId, ParentRole Role)> Members,
        (short? Year, byte? Month, byte? Day) Date);

    /// <summary>
    /// True for an <c>ADOP</c> event that names a family in the file. It becomes (part of) an adopted set, so it is
    /// not also kept as a timeline event. An <c>ADOP</c> with no family stays an ordinary event.
    /// </summary>
    private static bool IsAdoptionIntoFamily(GedcomEvent gedcomEvent, IndividualRecord individual) =>
        gedcomEvent.Tag == GedcomTags.ADOP
        && gedcomEvent.FamilyXref is { } xref
        && individual.Structure.File?.Find<FamilyRecord>(xref) is not null;

    private async Task ImportParentSetsAsync(
        GedcomSharp.GedcomFile dataset,
        Dictionary<string, int> xrefToPersonId,
        GedcomImportResult summary,
        MediaImportContext context)
    {
        var storedSets = (await setRepository.FindAsync(new SearchOptions<ParentSet>())).ToList();
        var storedMembers = (await memberRepository.FindAsync(new SearchOptions<ParentSetMember>())).ToLookup(member => member.ParentSetId);
        var people = (await personRepository.FindAsync(new SearchOptions<Person>())).ToList();
        var births = (await eventRepository.FindAsync(new SearchOptions<PersonEvent> { Query = evt => evt.EventType == PersonEventType.Birth })).ToList();
        var validator = new ParentSetValidator(
            storedSets.Select(set => (set.ChildPersonId, storedMembers[set.Id].Select(member => member.ParentPersonId))),
            births,
            people);

        var byXref = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach ((string xref, int id) in xrefToPersonId)
        {
            byXref[XrefKey(xref)] = id;
        }

        foreach (var individual in dataset.Individuals)
        {
            if (individual.Xref is null || !byXref.TryGetValue(XrefKey(individual.Xref), out int childId))
            {
                continue;
            }

            string who = $"{individual.DisplayName} ({XrefKey(individual.Xref)})".Trim();
            var accepted = new List<ImportedSet>();
            foreach (var link in CollectParentLinks(individual))
            {
                var imported = ResolveParentLink(dataset, link, byXref, childId, who, validator, accepted, context);
                if (imported is not null)
                {
                    accepted.Add(imported);
                    validator.Add(childId, imported.Members.Select(member => member.PersonId));
                }
            }

            if (accepted.Count > 0)
            {
                await SaveImportedSetsAsync(childId, accepted, storedSets, storedMembers, summary);
            }
        }
    }

    /// <summary>
    /// One entry per family the person points at. A <c>FAMC</c> and an <c>ADOP</c> event that name the same family are
    /// the same adoption, because some programs write only one of the two.
    /// </summary>
    private static List<ParentLink> CollectParentLinks(IndividualRecord individual)
    {
        var links = new List<ParentLink>();
        ParentLink Link(string xref, int order)
        {
            var existing = links.FirstOrDefault(link => SameXref(link.FamilyXref, xref));
            if (existing is not null)
            {
                return existing;
            }

            var added = new ParentLink(XrefKey(xref), order);
            links.Add(added);
            return added;
        }

        int order = 0;
        foreach (var famc in individual.ChildToFamilyLinks)
        {
            if (famc.FamilyXref is not { Length: > 0 } xref)
            {
                continue;
            }

            var link = Link(xref, order++);
            link.HasLink = true;
            link.Pedigree ??= famc.Pedigree;
            link.Primary |= string.Equals(famc.Structure.ChildValue(PrimaryFamilyTag)?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);
        }

        foreach (var adoption in individual.GetEvents(GedcomTags.ADOP))
        {
            if (adoption.FamilyXref is not { Length: > 0 } xref)
            {
                continue;
            }

            var link = Link(xref, order++);
            link.HasAdoption = true;
            link.Adopter ??= adoption.Structure.Child("FAMC")?.ChildValue("ADOP")?.Trim().ToUpperInvariant();
            var date = ParseDate(adoption.Date);
            if (date.Year is not null)
            {
                link.Date = date;
            }
        }

        return links;
    }

    /// <summary>
    /// Turns one link into a set, or explains in the import report why it was skipped. The set passes the same checks
    /// as one added by hand: one or two parents, no loops, nobody born before their parent.
    /// </summary>
    private ImportedSet? ResolveParentLink(
        GedcomSharp.GedcomFile dataset,
        ParentLink link,
        Dictionary<string, int> byXref,
        int childId,
        string who,
        ParentSetValidator validator,
        List<ImportedSet> accepted,
        MediaImportContext context)
    {
        var type = ParentageType.Birth;
        if (link.HasAdoption)
        {
            type = ParentageType.Adopted;
        }
        else if (!string.IsNullOrEmpty(link.Pedigree))
        {
            switch (link.Pedigree)
            {
                case "BIRTH":
                    break;
                case "ADOPTED":
                    type = ParentageType.Adopted;
                    break;
                case "FOSTER":
                    type = ParentageType.Foster;
                    break;
                default:
                    context.Warn($"{who}: skipped the link to family {link.FamilyXref} because the pedigree '{link.Pedigree.ToLowerInvariant()}' is not supported. Only birth, adopted and foster are.");
                    return null;
            }
        }

        var family = dataset.Find<FamilyRecord>(link.FamilyXref);
        if (family is null)
        {
            context.Warn($"{who}: skipped the link to family {link.FamilyXref} because that family is not in the file.");
            return null;
        }

        int? husband = family.HusbandXref is { } husbandXref && byXref.TryGetValue(XrefKey(husbandXref), out int hid) ? hid : null;
        int? wife = family.WifeXref is { } wifeXref && byXref.TryGetValue(XrefKey(wifeXref), out int wid) ? wid : null;

        // An ADOP event can say that only one of the couple adopted. The narrower list wins, unless it names someone
        // the family does not have.
        if (link.HasAdoption)
        {
            if (link.Adopter == "HUSB" && husband is not null)
            {
                wife = null;
            }
            else if (link.Adopter == "WIFE" && wife is not null)
            {
                husband = null;
            }
        }

        var members = new List<(int PersonId, ParentRole Role)>();
        if (husband is int father && father != childId)
        {
            members.Add((father, ParentRole.Father));
        }

        if (wife is int mother && mother != childId && mother != husband)
        {
            members.Add((mother, ParentRole.Mother));
        }

        if (members.Count == 0)
        {
            context.Warn($"{who}: skipped the link to family {link.FamilyXref} because it has no parents in the file.");
            return null;
        }

        if (accepted.Any(other => other.Type == type && other.Members.Select(member => member.PersonId).Order().SequenceEqual(members.Select(member => member.PersonId).Order())))
        {
            return null;
        }

        PartialDate? date = null;
        if (type != ParentageType.Birth && link.Date.Year is short year && PartialDate.TryCreate(year, link.Date.Month, link.Date.Month is null ? null : link.Date.Day, out var parsed))
        {
            date = parsed;
        }

        string? problem = validator.Check(childId, members.Select(member => member.PersonId).ToList(), date);
        if (problem is not null)
        {
            context.Warn($"{who}: skipped the {ParentageTypes.Label(type).ToLowerInvariant()} link to family {link.FamilyXref}. {problem}");
            return null;
        }

        var kept = type == ParentageType.Birth || date is null ? (default(short?), default(byte?), default(byte?)) : link.Date;
        return new ImportedSet(link, type, members, kept);
    }

    private async Task SaveImportedSetsAsync(
        int childId,
        List<ImportedSet> accepted,
        List<ParentSet> storedSets,
        ILookup<int, ParentSetMember> storedMembers,
        GedcomImportResult summary)
    {
        var existing = storedSets.Where(set => set.ChildPersonId == childId).ToList();
        bool hadPrimary = existing.Any(set => set.IsPrimary);
        bool fileChoosesPrimary = accepted.Any(set => set.Link.Primary);
        var saved = new List<(ImportedSet Source, ParentSet Set)>();

        foreach (var imported in accepted)
        {
            var wanted = imported.Members.Select(member => member.PersonId).Order().ToList();
            var match = existing.FirstOrDefault(set =>
                set.Type == imported.Type
                && storedMembers[set.Id].Select(member => member.ParentPersonId).Order().SequenceEqual(wanted));

            if (match is not null)
            {
                // Importing the same file again. The date can improve, nothing else changes.
                if (imported.Date.Year is not null && (match.Year != imported.Date.Year || match.Month != imported.Date.Month || match.Day != imported.Date.Day))
                {
                    match.Year = imported.Date.Year;
                    match.Month = imported.Date.Month;
                    match.Day = imported.Date.Day;
                    await setRepository.UpdateAsync(match);
                }

                saved.Add((imported, match));
                continue;
            }

            var set = await setRepository.InsertAsync(new ParentSet
            {
                ChildPersonId = childId,
                Type = imported.Type,
                IsPrimary = false,
                Year = imported.Date.Year,
                Month = imported.Date.Month,
                Day = imported.Date.Day,
                Visibility = LinkVisibility.Everyone,
                CreatedAtUtc = DateTime.UtcNow,
            });

            foreach ((int personId, var role) in imported.Members)
            {
                await memberRepository.InsertAsync(new ParentSetMember { ParentSetId = set.Id, ParentPersonId = personId, Role = role });
            }

            existing.Add(set);
            storedSets.Add(set);
            saved.Add((imported, set));
            summary.ParentSetsImported++;
            summary.RelationshipsImported++;
        }

        // GEDCOM has no primary flag. The file's own tag wins. A child who already had a primary set keeps it when the
        // file says nothing. Otherwise the default rule decides: adopted, then birth, then foster, in file order.
        if (hadPrimary && !fileChoosesPrimary)
        {
            return;
        }

        var primary = saved.Where(item => item.Source.Link.Primary).Select(item => item.Set).FirstOrDefault()
            ?? saved.OrderBy(item => ParentGraph.DefaultRank(item.Source.Type)).ThenBy(item => item.Source.Link.Order).Select(item => item.Set).First();
        foreach (var set in existing.Where(set => set.IsPrimary != (set.Id == primary.Id)))
        {
            set.IsPrimary = set.Id == primary.Id;
            await setRepository.UpdateAsync(set);
        }
    }

    /// <summary>
    /// Writes every set of parents the export may include as a <c>FAM</c>, the child's <c>FAMC</c> (primary first, with
    /// <c>PEDI</c>), and an <c>ADOP</c> event for adopted sets. Marriages come from the spouse links as before.
    /// </summary>
    private static void ApplyFamilies(GedcomSharp.GedcomFile dataset, List<Person> people, List<PersonSpouse> spouses, ParentGraph graph)
    {
        var kept = new HashSet<GedcomStructure>();
        var byId = people.ToDictionary(person => person.Id);
        string XrefOf(Person person) => XrefKey(FormatXref(person.GedcomId, person.Id));

        // Children are rebuilt from the sets, so what an earlier import left behind is cleared first.
        foreach (var individual in dataset.Individuals)
        {
            individual.Structure.RemoveChildren("FAMC");
            foreach (var adoption in individual.Structure.ChildrenWithTag("ADOP").Where(adoption => adoption.Child("FAMC") is not null).ToList())
            {
                adoption.Remove();
            }
        }

        foreach (var family in dataset.Families)
        {
            family.Structure.RemoveChildren("CHIL");
        }

        FamilyRecord FamilyFor(Person? husband, Person? wife, ISet<GedcomStructure>? avoid)
        {
            string? husbandXref = husband is null ? null : XrefOf(husband);
            string? wifeXref = wife is null ? null : XrefOf(wife);
            var family = dataset.Families.FirstOrDefault(candidate =>
                SameXref(candidate.HusbandXref, husbandXref)
                && SameXref(candidate.WifeXref, wifeXref)
                && (avoid is null || !avoid.Contains(candidate.Structure)));
            family ??= dataset.AddFamily(dataset.Find<IndividualRecord>(husbandXref), dataset.Find<IndividualRecord>(wifeXref));
            kept.Add(family.Structure);
            return family;
        }

        foreach (var spouse in spouses)
        {
            byId.TryGetValue(spouse.PersonId, out var first);
            byId.TryGetValue(spouse.SpouseId, out var second);
            var husband = first?.IsMale == true ? first : second?.IsMale == true ? second : first;
            var wife = husband == first ? second : first;
            if (husband is null && wife is null)
            {
                continue;
            }

            var family = FamilyFor(husband, wife, avoid: null);
            SetFamilyDate(family, "MARR", spouse.MarriageYear, spouse.MarriageMonth, spouse.MarriageDay);
            SetFamilyDate(family, "DIV", spouse.DivorceYear, spouse.DivorceMonth, spouse.DivorceDay);
            if (spouse.HasEngagement || spouse.EngagementYear is not null)
            {
                SetFamilyDate(family, "ENGA", spouse.EngagementYear, spouse.EngagementMonth, spouse.EngagementDay);
            }
        }

        foreach (int childId in graph.Children.Order())
        {
            if (!byId.TryGetValue(childId, out var childPerson) || dataset.Find<IndividualRecord>(XrefOf(childPerson)) is not { } child)
            {
                continue;
            }

            var sets = graph.SetsOf(childId).Where(set => set.Members.All(member => byId.ContainsKey(member.PersonId))).ToList();
            var usedForThisChild = new HashSet<GedcomStructure>();
            foreach (var set in sets)
            {
                // The primary set comes first in the list, and most programs treat the first FAMC as the main one.
                var father = set.FatherId is int fatherId ? byId[fatherId] : null;
                var mother = set.MotherId is int motherId ? byId[motherId] : null;
                var family = FamilyFor(father, mother, usedForThisChild);
                usedForThisChild.Add(family.Structure);

                dataset.AddChildToFamily(family, child, PedigreeOf(set.Type));
                var link = child.ChildToFamilyLinks.Last();
                if (set.IsPrimary && sets.Count > 1)
                {
                    link.Structure.AddChild(PrimaryFamilyTag, "Y");
                }

                if (set.Type == ParentageType.Adopted)
                {
                    var adoption = child.Structure.AddChild("ADOP");
                    if (set.Year is short year)
                    {
                        adoption.SetChildValue("DATE", FormatDate(year, set.Month, set.Day));
                    }

                    adoption.AddPointer("FAMC", family.Xref!).AddChild("ADOP", set.Members.Count == 2 ? "BOTH" : set.FatherId is not null ? "HUSB" : "WIFE");
                }
            }
        }

        foreach (var family in dataset.Families.Where(family => !kept.Contains(family.Structure)).ToList())
        {
            dataset.Remove(family.Structure);
        }
    }

    /// <summary>The <c>PEDI</c> value. The writer lowers it for 5.5.1 and raises it for 7.0.</summary>
    private static string PedigreeOf(ParentageType type) => type switch
    {
        ParentageType.Adopted => "adopted",
        ParentageType.Foster => "foster",
        _ => "birth",
    };
}
