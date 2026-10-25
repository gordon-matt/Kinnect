using System.Text;
using System.Text.RegularExpressions;
using GedcomSharp.Text;
using Kinnect.Models.FamilyTree;

namespace Kinnect.Services;

public partial class GedcomService
{
    /// <summary>
    /// Family Tree Maker stores these as custom tags. The reader rewrites them to generic events, so the
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

    private async Task<string?> ReadStoredOriginalAsync(CancellationToken cancellationToken)
    {
        var files = (await gedcomFileRepository.FindAsync(new SearchOptions<GedcomFile>())).ToList();
        var file = files.OrderByDescending(candidate => candidate.Id).FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        string path = fileStorage.GetFullPath(file.StoragePath);
        if (!File.Exists(path))
        {
            logger.LogWarning("The stored GEDCOM {Path} is missing. A new file was written instead.", path);
            return null;
        }

        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return DecodeGedcom(bytes);
    }

    /// <summary>
    /// Writes Kinnect's current data over the matching parts of the uploaded file. Lines Kinnect does not
    /// model stay as they were. Returns null when the text is not a GEDCOM file.
    /// </summary>
    private string? PatchOriginal(
        string original,
        List<Person> people,
        List<PersonSpouse> spouses,
        ILookup<int, PersonEvent> eventsByPerson,
        ILookup<int, Contact> contactsByPerson,
        IReadOnlyList<StagedMedia> staged,
        GedcomExportOptions options,
        IProgress<GedcomExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var document = GedcomText.Parse(original);
        if (document.Records.Count == 0 || document.Records.All(record => record.Tag != "HEAD"))
        {
            return null;
        }

        var peopleByXref = people
            .Where(person => !string.IsNullOrWhiteSpace(person.GedcomId))
            .GroupBy(person => XrefKey(person.GedcomId))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var plans = options.Families ? PlanFamilies(document, people, spouses) : [];
        if (options.Families)
        {
            AttachFamilyEvents(plans, peopleByXref, eventsByPerson);
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int completed = 0;
        int total = Math.Max(1, people.Count);
        var output = new StringBuilder();

        foreach (var record in document.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record.Tag == "TRLR")
            {
                AppendNewPeople(output, people, seen, eventsByPerson, contactsByPerson, staged, options, document.Newline, progress, ref completed, total, cancellationToken);
                if (options.Families)
                {
                    AppendNewFamilies(output, plans, document.Newline);
                }

                output.Append(record.ToText(document.Newline));
                continue;
            }

            if (record.Tag == "INDI")
            {
                if (!peopleByXref.TryGetValue(record.Xref, out var person))
                {
                    continue;
                }

                seen.Add(record.Xref);
                PatchIndividual(record, person, eventsByPerson[person.Id].OrderBy(evt => evt.Id).ToList(), contactsByPerson, staged, plans, options, document);
                output.Append(record.ToText(document.Newline));
                completed++;
                progress?.Report(new GedcomExportProgress(completed, total, person.GivenNames));
                continue;
            }

            if (record.Tag == "FAM" && options.Families)
            {
                var plan = plans.FirstOrDefault(candidate => candidate.Record == record);
                if (plan is null)
                {
                    continue;
                }

                PatchFamily(record, plan, document);
                output.Append(record.ToText(document.Newline));
                continue;
            }

            output.Append(record.ToText(document.Newline));
        }

        if (document.Records.All(record => record.Tag != "TRLR"))
        {
            AppendNewPeople(output, people, seen, eventsByPerson, contactsByPerson, staged, options, document.Newline, progress, ref completed, total, cancellationToken);
            if (options.Families)
            {
                AppendNewFamilies(output, plans, document.Newline);
            }

            output.Append("0 TRLR").Append(document.Newline);
        }

        return output.ToString();
    }

    private void PatchIndividual(
        TextRecord record,
        Person person,
        List<PersonEvent> events,
        ILookup<int, Contact> contactsByPerson,
        IReadOnlyList<StagedMedia> staged,
        List<FamilyPlan> plans,
        GedcomExportOptions options,
        GedcomText document)
    {
        var blocks = SplitBlocks(record);
        if (options.Names)
        {
            PatchName(blocks, person);
        }

        if (options.Notes)
        {
            PatchNotes(blocks, person, document);
        }

        if (options.Events)
        {
            PatchEvents(blocks, events, plans, record.Xref, document);
        }

        if (options.Contacts)
        {
            blocks.RemoveAll(block => IsContactBlock(block));
            blocks.AddRange(ContactBlocks(contactsByPerson[person.Id]));
        }

        blocks.RemoveAll(block => block[0].Tag == "OBJE");
        blocks.AddRange(MediaBlocks(staged.Where(item => item.PersonId == person.Id)));

        if (options.Families)
        {
            PatchFamilyLinks(blocks, record.Xref, plans);
        }

        record.Lines = [record.Lines[0], .. blocks.SelectMany(block => block)];
    }

    private static void PatchName(List<List<GedcomLine>> blocks, Person person)
    {
        int index = blocks.FindIndex(block => block[0].Tag == "NAME");
        if (index < 0)
        {
            blocks.Insert(0, [Line(1, "NAME", $"{person.GivenNames} /{person.FamilyName}/")]);
            index = 0;
        }

        var name = blocks[index];
        string? given = ChildValue(name, "GIVN");
        string? surname = ChildValue(name, "SURN");
        bool givenSame = given is not null
            ? GivenNamesFrom(given) == person.GivenNames
            : NameGiven(name[0].Value) == person.GivenNames;
        bool surnameSame = (surname ?? NameSurname(name[0].Value)).Trim() == person.FamilyName;
        if (!givenSame || !surnameSame)
        {
            SetLine(name[0], 1, "NAME", $"{person.GivenNames} /{person.FamilyName}/");
            SetChild(name, "GIVN", person.GivenNames);
            SetChild(name, "SURN", person.FamilyName);
        }

        SetChild(name, "NPFX", person.NamePrefix);
        SetChild(name, "NICK", person.Nickname);
        SetChild(name, "NSFX", person.NameSuffix);

        string sex = person.IsMale switch { true => "M", false => "F", _ => "U" };
        int sexIndex = blocks.FindIndex(block => block[0].Tag == "SEX");
        if (sexIndex < 0)
        {
            blocks.Insert(index + 1, [Line(1, "SEX", sex)]);
        }
        else if (!string.Equals(blocks[sexIndex][0].Value.Trim(), sex, StringComparison.OrdinalIgnoreCase))
        {
            SetLine(blocks[sexIndex][0], 1, "SEX", sex);
        }
    }

    private void PatchNotes(List<List<GedcomLine>> blocks, Person person, GedcomText document)
    {
        var notes = blocks.Where(block => block[0].Tag == "NOTE").ToList();
        string current = JoinNoteBlocks(notes, document);
        string wanted = person.Note?.Replace("\r\n", "\n").Trim() ?? string.Empty;
        if (NotesMatch(current, wanted))
        {
            return;
        }

        blocks.RemoveAll(block => block[0].Tag == "NOTE");
        if (wanted.Length > 0)
        {
            blocks.Add(NoteBlock(1, wanted));
        }
    }

    private void PatchEvents(List<List<GedcomLine>> blocks, List<PersonEvent> events, List<FamilyPlan> plans, string xref, GedcomText document)
    {
        var familyTags = plans
            .Where(plan => plan.HusbandXref == xref || plan.WifeXref == xref)
            .SelectMany(plan => plan.EventTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownTags = events.Select(TagOf).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remaining = events.Where(evt => !PersonEventType.IsNonTimelineEventType(TagOf(evt))).ToList();
        var consumed = new HashSet<int>();

        for (int i = 0; i < blocks.Count; i++)
        {
            if (!IsEventBlock(blocks[i], knownTags) || IsContactBlock(blocks[i]))
            {
                continue;
            }

            string tag = blocks[i][0].Tag;
            var match = remaining.FirstOrDefault(evt => !consumed.Contains(evt.Id) && TagOf(evt).Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                blocks.RemoveAt(i);
                i--;
                continue;
            }

            consumed.Add(match.Id);
            if (!EventUnchanged(blocks[i], match, document))
            {
                blocks[i] = MergeEvent(blocks[i], match, document);
            }
        }

        foreach (var evt in remaining.Where(evt => !consumed.Contains(evt.Id)))
        {
            string tag = TagOf(evt);
            if (familyTags.Contains(tag))
            {
                continue;
            }

            blocks.Add(NewEventBlock(evt));
        }
    }

    private bool EventUnchanged(List<GedcomLine> block, PersonEvent evt, GedcomText document)
    {
        var parsed = ParseDateString(ChildValue(block, "DATE"));
        if (!SameDate(parsed, evt))
        {
            return false;
        }

        string place = ChildValue(block, "PLAC")?.Trim() ?? string.Empty;
        if (!string.Equals(place, evt.Place?.Trim() ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        string detail = BlockDetail(block);
        PersonEventType.TryReadStoredTag(evt.EventType, evt.Description, out _, out string? wanted);
        if (!string.Equals(detail, wanted?.Trim() ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        return NotesMatch(JoinNoteBlocks(NoteGroups(block), document), evt.Note?.Replace("\r\n", "\n").Trim() ?? string.Empty);
    }

    private List<GedcomLine> MergeEvent(List<GedcomLine> block, PersonEvent evt, GedcomText document)
    {
        var lines = block.ToList();
        PersonEventType.TryReadStoredTag(evt.EventType, evt.Description, out string tag, out string? detail);
        if (!string.IsNullOrWhiteSpace(lines[0].Value) && !tag.Equals(PersonEventType.Custom, StringComparison.OrdinalIgnoreCase))
        {
            SetLine(lines[0], 1, lines[0].Tag, detail);
        }
        else
        {
            SetChild(lines, "TYPE", detail);
        }

        var parsed = ParseDateString(ChildValue(lines, "DATE"));
        if (!SameDate(parsed, evt))
        {
            if (evt.Year is null)
            {
                RemoveChild(lines, "DATE");
            }
            else
            {
                SetChild(lines, "DATE", FormatDate(evt.Year, evt.Month, evt.Day));
            }
        }

        string place = evt.Place?.Trim() ?? string.Empty;
        string currentPlace = ChildValue(lines, "PLAC")?.Trim() ?? string.Empty;
        if (!string.Equals(place, currentPlace, StringComparison.Ordinal))
        {
            RemoveSubtree(lines, "PLAC");
            if (place.Length > 0 || (evt.Latitude is not null && evt.Longitude is not null))
            {
                lines.AddRange(PlaceLines(place, evt.Latitude, evt.Longitude));
            }
        }
        else if (evt.Latitude is not null && evt.Longitude is not null && lines.All(line => line.Tag != "MAP"))
        {
            int placeIndex = lines.FindIndex(line => line.Tag == "PLAC");
            var map = MapLines(evt.Latitude.Value, evt.Longitude.Value);
            lines.InsertRange(placeIndex < 0 ? lines.Count : placeIndex + 1, map);
        }

        string wantedNote = evt.Note?.Replace("\r\n", "\n").Trim() ?? string.Empty;
        if (!NotesMatch(JoinNoteBlocks(NoteGroups(lines), document), wantedNote))
        {
            RemoveSubtree(lines, "NOTE");
            if (wantedNote.Length > 0)
            {
                lines.AddRange(NoteBlock(2, wantedNote));
            }
        }

        return lines;
    }

    private static List<GedcomLine> NewEventBlock(PersonEvent evt)
    {
        PersonEventType.TryReadStoredTag(evt.EventType, evt.Description, out string tag, out string? detail);
        bool valueOnLine = ValueOnLine(tag);
        var lines = new List<GedcomLine> { Line(1, tag, valueOnLine ? detail : null) };
        if (!valueOnLine && !string.IsNullOrWhiteSpace(detail))
        {
            lines.Add(Line(2, "TYPE", detail));
        }

        if (evt.Year is not null)
        {
            lines.Add(Line(2, "DATE", FormatDate(evt.Year, evt.Month, evt.Day)));
        }

        lines.AddRange(PlaceLines(evt.Place, evt.Latitude, evt.Longitude));
        if (!string.IsNullOrWhiteSpace(evt.Note))
        {
            lines.AddRange(NoteBlock(2, evt.Note));
        }

        return lines;
    }

    private static void PatchFamilyLinks(List<List<GedcomLine>> blocks, string xref, List<FamilyPlan> plans)
    {
        var childOf = plans.Where(plan => plan.ChildXrefs.Contains(xref)).Select(plan => plan.Xref).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var spouseOf = plans.Where(plan => plan.HusbandXref == xref || plan.WifeXref == xref).Select(plan => plan.Xref).ToHashSet(StringComparer.OrdinalIgnoreCase);

        blocks.RemoveAll(block => block[0].Tag == "FAMC" && !childOf.Contains(XrefKey(block[0].Value)));
        blocks.RemoveAll(block => block[0].Tag == "FAMS" && !spouseOf.Contains(XrefKey(block[0].Value)));

        foreach (string family in childOf.Where(family => blocks.All(block => block[0].Tag != "FAMC" || !SameXref(block[0].Value, family))))
        {
            blocks.Add([Line(1, "FAMC", $"@{family}@")]);
        }

        foreach (string family in spouseOf.Where(family => blocks.All(block => block[0].Tag != "FAMS" || !SameXref(block[0].Value, family))))
        {
            blocks.Add([Line(1, "FAMS", $"@{family}@")]);
        }
    }

    private void PatchFamily(TextRecord record, FamilyPlan plan, GedcomText document)
    {
        var blocks = SplitBlocks(record);
        PatchFamilyDate(blocks, "MARR", plan.MarriageYear, plan.MarriageMonth, plan.MarriageDay);
        PatchFamilyDate(blocks, "DIV", plan.DivorceYear, plan.DivorceMonth, plan.DivorceDay);
        PatchFamilyDate(blocks, "ENGA", plan.EngagementYear, plan.EngagementMonth, plan.EngagementDay);

        var childBlocks = blocks.Where(block => block[0].Tag == "CHIL").ToList();
        blocks.RemoveAll(block => block[0].Tag == "CHIL");
        foreach (string child in plan.ChildXrefs)
        {
            var existing = childBlocks.FirstOrDefault(block => SameXref(block[0].Value, child));
            blocks.Add(existing ?? [Line(1, "CHIL", $"@{child}@")]);
        }

        foreach (var evt in plan.FamilyEvents)
        {
            int index = blocks.FindIndex(block => block[0].Tag.Equals(TagOf(evt), StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                continue;
            }

            if (!EventUnchanged(blocks[index], evt, document))
            {
                blocks[index] = MergeEvent(blocks[index], evt, document);
            }
        }

        record.Lines = [record.Lines[0], .. blocks.SelectMany(block => block)];
    }

    private void PatchFamilyDate(List<List<GedcomLine>> blocks, string tag, short? year, byte? month, byte? day)
    {
        int index = blocks.FindIndex(block => block[0].Tag == tag);
        if (year is null)
        {
            return;
        }

        if (index < 0)
        {
            blocks.Add([Line(1, tag, null), Line(2, "DATE", FormatDate(year, month, day))]);
            return;
        }

        var current = ParseDateString(ChildValue(blocks[index], "DATE"));
        if (!SameDate(current, year, month, day))
        {
            SetChild(blocks[index], "DATE", FormatDate(year, month, day));
        }
    }

    private List<FamilyPlan> PlanFamilies(GedcomText document, List<Person> people, List<PersonSpouse> spouses)
    {
        var byXref = people
            .Where(person => !string.IsNullOrWhiteSpace(person.GedcomId))
            .GroupBy(person => XrefKey(person.GedcomId))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var plans = new List<FamilyPlan>();
        var usedXrefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in document.Records.Where(record => record.Tag == "FAM"))
        {
            var blocks = SplitBlocks(record);
            string husband = XrefKey(blocks.FirstOrDefault(block => block[0].Tag == "HUSB")?[0].Value);
            string wife = XrefKey(blocks.FirstOrDefault(block => block[0].Tag == "WIFE")?[0].Value);
            if (!byXref.TryGetValue(husband, out var husbandPerson) || !byXref.TryGetValue(wife, out var wifePerson))
            {
                continue;
            }

            var spouse = spouses.FirstOrDefault(link => Pair(link) == Pair(husbandPerson.Id, wifePerson.Id));
            if (spouse is null)
            {
                continue;
            }

            string xref = string.IsNullOrEmpty(record.Xref) ? NextFamilyXref(usedXrefs) : record.Xref;
            usedXrefs.Add(xref);
            plans.Add(FamilyPlan.From(record, xref, husband, wife, spouse, ChildrenOf(people, husbandPerson.Id, wifePerson.Id), EventTags(blocks)));
        }

        foreach (var spouse in spouses)
        {
            if (plans.Any(plan => plan.Spouse == spouse))
            {
                continue;
            }

            var pair = people.Where(person => person.Id == spouse.PersonId || person.Id == spouse.SpouseId).ToList();
            if (pair.Count != 2)
            {
                continue;
            }

            var husband = pair.FirstOrDefault(person => person.IsMale == true) ?? pair[0];
            var wife = pair.First(person => person != husband);
            string xref = NextFamilyXref(usedXrefs);
            usedXrefs.Add(xref);
            string husbandXref = XrefKey(FormatXref(husband.GedcomId, husband.Id));
            string wifeXref = XrefKey(FormatXref(wife.GedcomId, wife.Id));
            plans.Add(FamilyPlan.From(null, xref, husbandXref, wifeXref, spouse, ChildrenOf(people, husband.Id, wife.Id), []));
        }

        return plans;
    }

    private void AppendNewPeople(
        StringBuilder output,
        List<Person> people,
        HashSet<string> seen,
        ILookup<int, PersonEvent> eventsByPerson,
        ILookup<int, Contact> contactsByPerson,
        IReadOnlyList<StagedMedia> staged,
        GedcomExportOptions options,
        string newline,
        IProgress<GedcomExportProgress>? progress,
        ref int completed,
        int total,
        CancellationToken cancellationToken)
    {
        foreach (var person in people.Where(person => !seen.Contains(XrefKey(person.GedcomId)) && !seen.Contains(XrefKey(FormatXref(person.GedcomId, person.Id)))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fresh = new StringBuilder();
            WriteFreshIndividual(fresh, person, options, eventsByPerson, contactsByPerson, staged);
            output.Append(fresh.ToString().Replace(Environment.NewLine, newline));
            completed++;
            progress?.Report(new GedcomExportProgress(completed, total, person.GivenNames));
        }
    }

    private void WriteFreshIndividual(
        StringBuilder sb,
        Person person,
        GedcomExportOptions options,
        ILookup<int, PersonEvent> eventsByPerson,
        ILookup<int, Contact> contactsByPerson,
        IReadOnlyList<StagedMedia> staged)
    {
        sb.AppendLine($"0 {FormatXref(person.GedcomId, person.Id)} INDI");
        if (options.Names)
        {
            sb.AppendLine($"1 NAME {person.GivenNames} /{person.FamilyName}/");
            WriteNamePart(sb, "NPFX", person.NamePrefix);
            WriteNamePart(sb, "NICK", person.Nickname);
            WriteNamePart(sb, "NSFX", person.NameSuffix);
            sb.AppendLine($"1 SEX {person.IsMale switch { true => "M", false => "F", _ => "U" }}");
        }

        if (!options.Events)
        {
            WritePersonTail(sb, person, options, contactsByPerson, staged);
            return;
        }

        if (!eventsByPerson[person.Id].Any(evt => evt.EventType == PersonEventType.Occupation)
            && !string.IsNullOrWhiteSpace(person.Occupation))
        {
            sb.AppendLine($"1 OCCU {person.Occupation}");
        }

        if (!eventsByPerson[person.Id].Any(evt => evt.EventType == PersonEventType.Education)
            && !string.IsNullOrWhiteSpace(person.Education))
        {
            sb.AppendLine($"1 EDUC {person.Education}");
        }

        if (!eventsByPerson[person.Id].Any(evt => evt.EventType == PersonEventType.Religion)
            && !string.IsNullOrWhiteSpace(person.Religion))
        {
            sb.AppendLine($"1 RELI {person.Religion}");
        }

        foreach (var evt in eventsByPerson[person.Id].OrderBy(evt => evt.Id).Where(evt => !PersonEventType.IsNonTimelineEventType(evt.EventType)))
        {
            foreach (var line in NewEventBlock(evt))
            {
                sb.AppendLine(line.Text);
            }
        }

        if (options.Notes && !string.IsNullOrWhiteSpace(person.Note))
        {
            WriteNote(sb, 1, person.Note);
        }

        if (options.Contacts)
        {
            foreach (var contact in contactsByPerson[person.Id].OrderBy(contact => contact.SortOrder))
            {
                if (!string.IsNullOrWhiteSpace(contact.Value))
                {
                    sb.AppendLine($"1 {ContactTypes.GedcomTag(contact.Type)} {SingleLine(contact.Value)}");
                }
            }
        }

        WriteMedia(sb, staged.Where(item => item.PersonId == person.Id));
    }

    private static void AppendNewFamilies(StringBuilder output, List<FamilyPlan> plans, string newline)
    {
        foreach (var plan in plans.Where(plan => plan.Record is null))
        {
            var fresh = new StringBuilder();
            fresh.AppendLine($"0 @{plan.Xref}@ FAM");
            if (!string.IsNullOrEmpty(plan.HusbandXref))
            {
                fresh.AppendLine($"1 HUSB @{plan.HusbandXref}@");
            }

            if (!string.IsNullOrEmpty(plan.WifeXref))
            {
                fresh.AppendLine($"1 WIFE @{plan.WifeXref}@");
            }

            if (plan.MarriageYear is not null)
            {
                fresh.AppendLine("1 MARR");
                fresh.AppendLine($"2 DATE {FormatDate(plan.MarriageYear, plan.MarriageMonth, plan.MarriageDay)}");
            }

            if (plan.DivorceYear is not null)
            {
                fresh.AppendLine("1 DIV");
                fresh.AppendLine($"2 DATE {FormatDate(plan.DivorceYear, plan.DivorceMonth, plan.DivorceDay)}");
            }

            if (plan.EngagementYear is not null || plan.HasEngagement)
            {
                fresh.AppendLine("1 ENGA");
                if (plan.EngagementYear is not null)
                {
                    fresh.AppendLine($"2 DATE {FormatDate(plan.EngagementYear, plan.EngagementMonth, plan.EngagementDay)}");
                }
            }

            foreach (string child in plan.ChildXrefs)
            {
                fresh.AppendLine($"1 CHIL @{child}@");
            }

            output.Append(fresh.ToString().Replace(Environment.NewLine, newline));
        }
    }

    private static List<FoldedTag> ReadFoldedTags(byte[] bytes)
    {
        var document = GedcomText.Parse(DecodeGedcom(bytes));
        var found = new List<FoldedTag>();
        foreach (var record in document.Records.Where(record => record.Tag == "INDI"))
        {
            foreach (var block in SplitBlocks(record))
            {
                if (block[0].Tag is "_MILT" or "_MDCL" or "_HEIG" or "_WEIG")
                {
                    found.Add(new FoldedTag(record.Xref, block[0].Tag, block[0].Value, ChildValue(block, "DATE"), ChildValue(block, "PLAC")));
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
        bool date = SameDate(parsed, evt);
        bool place = string.Equals(evt.Place?.Trim() ?? string.Empty, item.Place?.Trim() ?? string.Empty, StringComparison.Ordinal);
        return date && place ? 0 : date || place ? 1 : 2;
    }

    private static string DecodeGedcom(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        string header = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 4000));
        string charset = "UTF-8";
        foreach (string raw in header.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("1 CHAR ", StringComparison.OrdinalIgnoreCase))
            {
                charset = line[7..].Trim();
                break;
            }

            if (line.StartsWith("0 ", StringComparison.Ordinal) && !line.StartsWith("0 HEAD", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        Encoding encoding = charset.ToUpperInvariant() switch
        {
            "ANSEL" => AnselEncoding.Instance,
            "ANSI" => Encoding.GetEncoding(1252),
            "IBMPC" => Encoding.GetEncoding(437),
            "ASCII" => Encoding.ASCII,
            "UNICODE" => Encoding.Unicode,
            _ => Encoding.UTF8,
        };
        string text = encoding.GetString(bytes);
        if (!charset.Equals("UTF-8", StringComparison.OrdinalIgnoreCase)
            && !charset.Equals("UTF8", StringComparison.OrdinalIgnoreCase))
        {
            text = Regex.Replace(text, @"(?m)^(1 CHAR\s+).*$", "${1}UTF-8", RegexOptions.IgnoreCase);
        }

        return text;
    }

    private static string FormatXref(string? gedcomId, int personId)
    {
        string raw = string.IsNullOrWhiteSpace(gedcomId) ? $"I{personId}" : gedcomId.Trim().Trim('@');
        return $"@{raw}@";
    }

    private static string XrefKey(string? xref) => xref?.Trim().Trim('@') ?? string.Empty;

    private static bool SameXref(string? left, string? right) =>
        string.Equals(XrefKey(left), XrefKey(right), StringComparison.OrdinalIgnoreCase);

    private static string TagOf(PersonEvent evt)
    {
        PersonEventType.TryReadStoredTag(evt.EventType, evt.Description, out string tag, out _);
        return tag;
    }

    private static bool ValueOnLine(string tag) => tag.ToUpperInvariant() is
        "OCCU" or "EDUC" or "RELI" or "TITL" or "DSCR" or "IDNO" or "NATI" or "NCHI" or "NMR" or "PROP" or "SSN" or "CAST" or "FACT";

    private static bool IsEventBlock(List<GedcomLine> block, IReadOnlySet<string> knownTags)
    {
        string tag = block[0].Tag;
        if (tag is "NAME" or "SEX" or "NOTE" or "OBJE" or "FAMC" or "FAMS" or "CHAN" or "REFN" or "RIN" or "AFN" or "RFN"
            or "ALIA" or "ASSO" or "SOUR" or "SUBM" or "ANCI" or "DESI" or "RESN" or "EMAIL" or "PHON" or "FAX" or "WWW" or "ADDR")
        {
            return false;
        }

        if (knownTags.Contains(tag) || tag.StartsWith('_') || tag.Equals(PersonEventType.Custom, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string label = PersonEventType.GetLabel(tag);
        return label != $"Custom Event ({tag})" && label != "Custom Event";
    }

    private static bool IsContactBlock(List<GedcomLine> block)
    {
        if (block[0].Tag is "EMAIL" or "PHON" or "FAX" or "WWW" or "ADDR")
        {
            return true;
        }

        return block[0].Tag == "RESI"
            && block.Any(line => line.Tag == "ADDR")
            && block.All(line => line.Tag is not ("DATE" or "PLAC"));
    }

    private static bool SameDate((short? Year, byte? Month, byte? Day) parsed, PersonEvent evt) =>
        SameDate(parsed, evt.Year, evt.Month, evt.Day);

    private static bool SameDate((short? Year, byte? Month, byte? Day) parsed, short? year, byte? month, byte? day)
    {
        if (parsed.Year != year)
        {
            return false;
        }

        if (parsed.Month is null && month is 1 && day is 1)
        {
            return true;
        }

        if (parsed.Month == month && parsed.Day is null && day is 1)
        {
            return true;
        }

        return parsed.Month == month && parsed.Day == day;
    }

    private static bool NotesMatch(string current, string wanted)
    {
        if (string.Equals(current, wanted, StringComparison.Ordinal))
        {
            return true;
        }

        return current.Length > 4000 && wanted.Length == 4000 && current.StartsWith(wanted, StringComparison.Ordinal);
    }

    private static string BlockDetail(List<GedcomLine> block)
    {
        if (!string.IsNullOrWhiteSpace(block[0].Value)
            && !block[0].Tag.Equals(PersonEventType.Custom, StringComparison.OrdinalIgnoreCase))
        {
            return block[0].Value.Trim();
        }

        return ChildValue(block, "TYPE")?.Trim() ?? string.Empty;
    }

    private static string JoinNoteBlocks(List<List<GedcomLine>> notes, GedcomText document)
    {
        var parts = new List<string>();
        foreach (var note in notes)
        {
            if (note.Count == 0)
            {
                continue;
            }

            string text = NoteText(note, document);
            if (text.Length > 0)
            {
                parts.Add(text);
            }
        }

        return string.Join("\n\n", parts);
    }

    private static string NoteText(List<GedcomLine> note, GedcomText document)
    {
        if (note[0].Value.StartsWith('@') && note[0].Value.EndsWith('@'))
        {
            var record = document.Records.FirstOrDefault(candidate =>
                candidate.Tag == "NOTE" && SameXref(candidate.Xref, note[0].Value));
            if (record is not null)
            {
                return RecordNote(record);
            }
        }

        var text = new StringBuilder(note[0].Value);
        foreach (var line in note.Skip(1))
        {
            if (line.Tag == "CONT")
            {
                text.Append('\n').Append(line.Value);
            }
            else if (line.Tag == "CONC")
            {
                text.Append(line.Value);
            }
        }

        return text.ToString().Trim();
    }

    private static string RecordNote(TextRecord record)
    {
        var text = new StringBuilder(record.Lines[0].Value);
        foreach (var line in record.Lines.Skip(1))
        {
            if (line.Level != 1)
            {
                continue;
            }

            if (line.Tag == "CONT")
            {
                text.Append('\n').Append(line.Value);
            }
            else if (line.Tag == "CONC")
            {
                text.Append(line.Value);
            }
        }

        return text.ToString().Trim();
    }

    private static List<List<GedcomLine>> NoteGroups(List<GedcomLine> block)
    {
        var groups = new List<List<GedcomLine>>();
        for (int i = 0; i < block.Count; i++)
        {
            if (block[i].Level != block[0].Level + 1 || block[i].Tag != "NOTE")
            {
                continue;
            }

            var group = new List<GedcomLine> { block[i] };
            int level = block[i].Level;
            for (int next = i + 1; next < block.Count && block[next].Level > level; next++)
            {
                group.Add(block[next]);
            }

            groups.Add(group);
        }

        return groups;
    }

    private static List<GedcomLine> NoteBlock(int level, string text)
    {
        string[] rows = text.Replace("\r\n", "\n").Split('\n');
        var lines = new List<GedcomLine> { Line(level, "NOTE", rows[0]) };
        foreach (string row in rows.Skip(1))
        {
            lines.Add(Line(level + 1, "CONT", row));
        }

        return lines;
    }

    private static List<GedcomLine> PlaceLines(string? place, double? latitude, double? longitude)
    {
        if (string.IsNullOrWhiteSpace(place) && (latitude is null || longitude is null))
        {
            return [];
        }

        var lines = new List<GedcomLine> { Line(2, "PLAC", place?.Trim()) };
        if (latitude is double lat && longitude is double lng)
        {
            lines.AddRange(MapLines(lat, lng));
        }

        return lines;
    }

    private static List<GedcomLine> MapLines(double latitude, double longitude) =>
    [
        Line(3, "MAP", null),
        Line(4, "LATI", GedcomCoordinates.FormatLatitude(latitude)),
        Line(4, "LONG", GedcomCoordinates.FormatLongitude(longitude)),
    ];

    private static List<List<GedcomLine>> ContactBlocks(IEnumerable<Contact> contacts)
    {
        var blocks = new List<List<GedcomLine>>();
        foreach (var contact in contacts.OrderBy(contact => contact.SortOrder))
        {
            if (!string.IsNullOrWhiteSpace(contact.Value))
            {
                blocks.Add([Line(1, ContactTypes.GedcomTag(contact.Type), SingleLine(contact.Value))]);
            }
        }

        return blocks;
    }

    private static List<List<GedcomLine>> MediaBlocks(IEnumerable<StagedMedia> items)
    {
        var blocks = new List<List<GedcomLine>>();
        foreach (var item in items)
        {
            var lines = new List<GedcomLine>
            {
                Line(1, "OBJE", null),
                Line(2, "FILE", item.ZipPath),
            };
            if (!string.IsNullOrWhiteSpace(item.Form))
            {
                lines.Add(Line(2, "FORM", item.Form));
            }

            if (!string.IsNullOrWhiteSpace(item.Title))
            {
                lines.Add(Line(2, "TITL", SingleLine(item.Title)));
            }

            if (!string.IsNullOrWhiteSpace(item.Date))
            {
                lines.Add(Line(2, "_DATE", SingleLine(item.Date)));
            }

            if (!string.IsNullOrWhiteSpace(item.Text))
            {
                lines.AddRange(Continued(2, "_TEXT", item.Text));
            }

            blocks.Add(lines);
        }

        return blocks;
    }

    private static List<GedcomLine> Continued(int level, string tag, string text)
    {
        string[] rows = text.Replace("\r\n", "\n").Split('\n');
        var lines = new List<GedcomLine> { Line(level, tag, rows[0]) };
        foreach (string row in rows.Skip(1))
        {
            lines.Add(Line(level + 1, "CONT", row));
        }

        return lines;
    }

    private static List<List<GedcomLine>> SplitBlocks(TextRecord record)
    {
        var blocks = new List<List<GedcomLine>>();
        List<GedcomLine>? current = null;
        foreach (var line in record.Lines.Skip(1))
        {
            if (line.Level <= 1 || current is null)
            {
                current = [line];
                blocks.Add(current);
            }
            else
            {
                current.Add(line);
            }
        }

        return blocks;
    }

    private static string? ChildValue(List<GedcomLine> block, string tag) =>
        block.FirstOrDefault(line => line.Level == block[0].Level + 1 && line.Tag == tag)?.Value;

    private static List<GedcomLine> LinesUnder(List<GedcomLine> block, string tag)
    {
        int index = block.FindIndex(line => line.Level == block[0].Level + 1 && line.Tag == tag);
        if (index < 0)
        {
            return [];
        }

        var lines = new List<GedcomLine> { block[index] };
        for (int i = index + 1; i < block.Count && block[i].Level > block[index].Level; i++)
        {
            lines.Add(block[i]);
        }

        return lines;
    }

    private static void SetChild(List<GedcomLine> block, string tag, string? value)
    {
        int index = block.FindIndex(line => line.Level == block[0].Level + 1 && line.Tag == tag);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (index >= 0)
            {
                RemoveSubtree(block, tag);
            }

            return;
        }

        if (index < 0)
        {
            block.Insert(1, Line(block[0].Level + 1, tag, value.Trim()));
            return;
        }

        if (!string.Equals(block[index].Value.Trim(), value.Trim(), StringComparison.Ordinal))
        {
            SetLine(block[index], block[index].Level, tag, value.Trim());
        }
    }

    private static void RemoveChild(List<GedcomLine> block, string tag) => RemoveSubtree(block, tag);

    private static void RemoveSubtree(List<GedcomLine> block, string tag)
    {
        int index = block.FindIndex(line => line.Level == block[0].Level + 1 && line.Tag == tag);
        if (index < 0)
        {
            return;
        }

        int end = index + 1;
        while (end < block.Count && block[end].Level > block[index].Level)
        {
            end++;
        }

        block.RemoveRange(index, end - index);
    }

    private static string NameGiven(string name)
    {
        int slash = name.IndexOf('/');
        string given = slash < 0 ? name : name[..slash];
        return GivenNamesFrom(given);
    }

    private static string NameSurname(string name)
    {
        int start = name.IndexOf('/');
        if (start < 0)
        {
            return string.Empty;
        }

        int end = name.IndexOf('/', start + 1);
        return end < 0 ? name[(start + 1)..].Trim() : name[(start + 1)..end].Trim();
    }

    private static List<string> ChildrenOf(List<Person> people, int parentA, int parentB) =>
        people
            .Where(person =>
                (person.FatherId == parentA && person.MotherId == parentB)
                || (person.FatherId == parentB && person.MotherId == parentA))
            .Select(person => XrefKey(FormatXref(person.GedcomId, person.Id)))
            .ToList();

    private static HashSet<string> EventTags(List<List<GedcomLine>> blocks)
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return blocks.Where(block => IsEventBlock(block, known)).Select(block => block[0].Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void AttachFamilyEvents(
        List<FamilyPlan> plans,
        Dictionary<string, Person> peopleByXref,
        ILookup<int, PersonEvent> eventsByPerson)
    {
        foreach (var plan in plans)
        {
            foreach (string xref in new[] { plan.HusbandXref, plan.WifeXref })
            {
                if (!peopleByXref.TryGetValue(xref, out var person))
                {
                    continue;
                }

                foreach (var evt in eventsByPerson[person.Id])
                {
                    string tag = TagOf(evt);
                    if (plan.EventTags.Contains(tag) && plan.FamilyEvents.All(existing => !TagOf(existing).Equals(tag, StringComparison.OrdinalIgnoreCase)))
                    {
                        plan.FamilyEvents.Add(evt);
                    }
                }
            }
        }
    }

    private static (int Low, int High) Pair(PersonSpouse link) => Pair(link.PersonId, link.SpouseId);

    private static (int Low, int High) Pair(int left, int right) =>
        left < right ? (left, right) : (right, left);

    private static string NextFamilyXref(HashSet<string> used)
    {
        int number = 1;
        while (!used.Add($"F{number}"))
        {
            number++;
        }

        used.Remove($"F{number}");
        return $"F{number}";
    }

    private static GedcomLine Line(int level, string tag, string? value)
    {
        var line = new GedcomLine();
        SetLine(line, level, tag, value);
        return line;
    }

    private static void SetLine(GedcomLine line, int level, string tag, string? value)
    {
        line.Level = level;
        line.Tag = tag;
        line.Value = value?.Trim() ?? string.Empty;
        line.Text = string.IsNullOrEmpty(line.Value) ? $"{level} {tag}" : $"{level} {tag} {line.Value}";
    }

    private sealed class GedcomLine
    {
        public int Level { get; set; }

        public string Tag { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }

    private sealed class TextRecord
    {
        public List<GedcomLine> Lines { get; set; } = [];

        public string Tag { get; set; } = string.Empty;

        public string Xref { get; set; } = string.Empty;

        public string ToText(string newline) => string.Join(newline, Lines.Select(line => line.Text)) + newline;
    }

    private sealed class GedcomText
    {
        public string Newline { get; init; } = "\n";

        public List<TextRecord> Records { get; init; } = [];

        public static GedcomText Parse(string text)
        {
            string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var records = new List<TextRecord>();
            TextRecord? current = null;
            foreach (string raw in text.Split('\n'))
            {
                string row = raw.TrimEnd('\r');
                if (row.Length == 0)
                {
                    continue;
                }

                if (!TryParseLine(row, out int level, out string? xref, out string tag, out string value))
                {
                    current?.Lines.Add(new GedcomLine { Level = 0, Tag = string.Empty, Text = row, Value = row });
                    continue;
                }

                if (level == 0)
                {
                    current = new TextRecord { Tag = tag, Xref = xref ?? string.Empty };
                    records.Add(current);
                }

                current?.Lines.Add(new GedcomLine { Level = level, Tag = tag, Value = value, Text = row });
            }

            return new GedcomText { Newline = newline, Records = records };
        }

        private static bool TryParseLine(string row, out int level, out string? xref, out string tag, out string value)
        {
            level = 0;
            xref = null;
            tag = string.Empty;
            value = string.Empty;
            var match = LinePattern.Match(row);
            if (!match.Success)
            {
                return false;
            }

            level = int.Parse(match.Groups[1].Value);
            xref = match.Groups[2].Success ? match.Groups[2].Value : null;
            tag = match.Groups[3].Value;
            value = match.Groups[4].Success ? match.Groups[4].Value : string.Empty;
            return true;
        }

        private static readonly Regex LinePattern = new(
            @"^(\d+)\s+(?:@([^@]+)@\s+)?([A-Za-z0-9_]+)(?:\s(.*))?$",
            RegexOptions.CultureInvariant);
    }

    private sealed record FoldedTag(string Xref, string Tag, string? LineValue, string? Date, string? Place);

    private sealed record FamilyPlan(
        TextRecord? Record,
        string Xref,
        string HusbandXref,
        string WifeXref,
        PersonSpouse Spouse,
        HashSet<string> ChildXrefs,
        HashSet<string> EventTags,
        short? MarriageYear,
        byte? MarriageMonth,
        byte? MarriageDay,
        short? DivorceYear,
        byte? DivorceMonth,
        byte? DivorceDay,
        short? EngagementYear,
        byte? EngagementMonth,
        byte? EngagementDay,
        bool HasEngagement,
        List<PersonEvent> FamilyEvents)
    {
        public static FamilyPlan From(
            TextRecord? record,
            string xref,
            string husband,
            string wife,
            PersonSpouse spouse,
            List<string> children,
            HashSet<string> eventTags) => new(
                record,
                xref,
                husband,
                wife,
                spouse,
                children.ToHashSet(StringComparer.OrdinalIgnoreCase),
                eventTags,
                spouse.MarriageYear,
                spouse.MarriageMonth,
                spouse.MarriageDay,
                spouse.DivorceYear,
                spouse.DivorceMonth,
                spouse.DivorceDay,
                spouse.EngagementYear,
                spouse.EngagementMonth,
                spouse.EngagementDay,
                spouse.HasEngagement,
                []);
    }
}
