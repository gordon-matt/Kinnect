namespace Kinnect.Services;

public partial class GedcomService
{
    /// <summary>
    /// Family Tree Maker stores these as custom tags. Some readers fold them into a generic event, so the
    /// original tag is read from the file and put back on the event Kinnect saved.
    /// </summary>
    private async Task RestoreFoldedTagsAsync(string gedcomPath)
    {
        var folded = ReadFoldedTags(await File.ReadAllBytesAsync(gedcomPath));
        if (folded.Count == 0)
        {
            return;
        }

        var people = (await personRepository.FindAsync(new SearchOptions<Person>())).ToList();
        var events = (await eventRepository.FindAsync(new SearchOptions<PersonEvent>())).ToList();
        var used = new HashSet<int>();
        foreach (var item in folded)
        {
            var person = people.FirstOrDefault(candidate => SameXref(candidate.GedcomId, item.Xref));
            if (person is null)
            {
                continue;
            }

            var match = events
                .Where(evt => evt.PersonId == person.Id && !used.Contains(evt.Id) && FoldedTagMatches(evt, item))
                .OrderBy(evt => FoldedTagScore(evt, item))
                .FirstOrDefault();
            if (match is null)
            {
                continue;
            }

            used.Add(match.Id);
            match.EventType = item.Tag;
            if (match.Description is "Military Service" or "Medical" or "Height" or "Weight")
            {
                match.Description = string.IsNullOrWhiteSpace(item.LineValue) ? null : Truncate(item.LineValue.Trim(), 2000);
            }

            await eventRepository.UpdateAsync(match);
        }
    }

    private static List<FoldedTag> ReadFoldedTags(byte[] bytes)
    {
        var read = new GedcomSharp.GedcomReader().Read(bytes);
        if (!read.IsSuccess)
        {
            return [];
        }

        var found = new List<FoldedTag>();
        foreach (var individual in read.Value.Individuals)
        {
            foreach (var child in individual.Structure.Children)
            {
                if (child.Tag is "_MILT" or "_MDCL" or "_HEIG" or "_WEIG")
                {
                    found.Add(new FoldedTag(individual.Xref ?? string.Empty, child.Tag, child.Value, child.ChildValue("DATE"), child.ChildValue("PLAC")));
                }
            }
        }

        return found;
    }

    private static bool FoldedTagMatches(PersonEvent evt, FoldedTag item) => item.Tag switch
    {
        "_MILT" => (evt.EventType is "EVEN" or "_MILT")
            && (evt.Description is null or "Military Service" || evt.Description == item.LineValue),
        _ => evt.EventType is "FACT" or "_MDCL" or "_HEIG" or "_WEIG"
            && (evt.Description is null or "Medical" or "Height" or "Weight" || evt.Description == item.LineValue),
    };

    private int FoldedTagScore(PersonEvent evt, FoldedTag item)
    {
        var parsed = ParseDateString(item.Date);
        bool date = SameDate(parsed, evt.Year, evt.Month, evt.Day);
        bool place = string.Equals(evt.Place?.Trim() ?? string.Empty, item.Place?.Trim() ?? string.Empty, StringComparison.Ordinal);
        return date && place ? 0 : date || place ? 1 : 2;
    }

    private sealed record FoldedTag(string Xref, string Tag, string? LineValue, string? Date, string? Place);
}