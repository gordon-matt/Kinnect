using GedcomSharp.Gedzip;
using GedcomSharp.Model;
using Kinnect.Models.FamilyTree;
using GedcomStructure = GedcomSharp.GedcomStructure;
using GedcomSex = GedcomSharp.Values.GedcomSex;
using GedcomVersion = GedcomSharp.GedcomVersion;

namespace Kinnect.Services;

public partial class GedcomService
{
    private static readonly HashSet<string> managedEventTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIRT", "CHR", "DEAT", "BURI", "CREM", "ADOP", "BAPM", "BARM", "BASM", "BLES", "CHRA", "CONF", "FCOM", "ORDN",
        "NATU", "EMIG", "IMMI", "CENS", "PROB", "WILL", "GRAD", "RETI", "EVEN", "CAST", "DSCR", "EDUC", "IDNO", "NATI",
        "NCHI", "NMR", "OCCU", "PROP", "RELI", "RESI", "SSN", "TITL", "FACT",
    };

    private static readonly HashSet<string> valueOnLineTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "OCCU", "EDUC", "RELI", "TITL", "DSCR", "IDNO", "NATI", "NCHI", "NMR", "PROP", "SSN", "CAST", "FACT",
    };

    private static readonly string[] contactTags = ["EMAIL", "EMAI", "_EMAIL", "PHON", "FAX", "_FAX", "WWW", "_WWW", "_URL", "ADDR"];

    public async Task<string?> ImportedVersionAsync(CancellationToken cancellationToken = default)
    {
        var dataset = await ReadStoredDatasetAsync(cancellationToken);
        return dataset?.Version switch
        {
            GedcomVersion.V55 => "5.5",
            GedcomVersion.V555 => "5.5.5",
            GedcomVersion.V70 => "7.0",
            GedcomVersion.V551 => "5.5.1",
            null => null,
            _ => "5.5.1",
        };
    }

    private async Task<string> BuildGedcomAsync(
        GedcomExportOptions options,
        IProgress<GedcomExportProgress>? progress,
        IReadOnlyList<StagedMedia> staged,
        CancellationToken cancellationToken)
    {
        var dataset = await BuildDatasetAsync(options, progress, staged, cancellationToken);
        var written = Writer(options, dataset).WriteToString(dataset);
        if (!written.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(" ", written.ValidationErrors.Select(error => error.ErrorMessage).Concat(written.Errors)));
        }

        return written.Value;
    }

    private async Task<GedcomSharp.GedcomFile> BuildDatasetAsync(
        GedcomExportOptions options,
        IProgress<GedcomExportProgress>? progress,
        IReadOnlyList<StagedMedia> staged,
        CancellationToken cancellationToken)
    {
        var version = ParseExportVersion(options.Version);
        var people = (await personRepository.FindAsync(new SearchOptions<Person>())).ToList();
        var spouses = (await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>())).ToList();
        var events = (await eventRepository.FindAsync(new SearchOptions<PersonEvent>())).ToLookup(evt => evt.PersonId);
        var contacts = (await contactRepository.FindAsync(new SearchOptions<Contact>())).ToLookup(contact => contact.PersonId);

        var stored = await ReadStoredDatasetAsync(cancellationToken);
        var dataset = stored ?? GedcomSharp.GedcomFile.Create(version, "Kinnect");
        var byXref = people
            .Where(person => !string.IsNullOrWhiteSpace(person.GedcomId))
            .GroupBy(person => XrefKey(person.GedcomId))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        int completed = 0;
        int total = Math.Max(1, people.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var individual in dataset.Individuals.ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (individual.Xref is null || !byXref.TryGetValue(individual.Xref, out var person))
            {
                dataset.Remove(individual.Structure);
                continue;
            }

            seen.Add(individual.Xref);
            ApplyIndividual(individual, person, events[person.Id].OrderBy(evt => evt.Id).ToList(), contacts, staged, options);
            completed++;
            progress?.Report(new GedcomExportProgress(completed, total, person.GivenNames));
        }

        foreach (var person in people.Where(person => !seen.Contains(XrefKey(FormatXref(person.GedcomId, person.Id)))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var individual = dataset.AddIndividual(XrefKey(FormatXref(person.GedcomId, person.Id)));
            ApplyIndividual(individual, person, events[person.Id].OrderBy(evt => evt.Id).ToList(), contacts, staged, options);
            completed++;
            progress?.Report(new GedcomExportProgress(completed, total, person.GivenNames));
        }

        if (options.Families)
        {
            ApplyFamilies(dataset, people, spouses);
        }

        return dataset;
    }

    private static void ApplyIndividual(
        IndividualRecord individual,
        Person person,
        List<PersonEvent> events,
        ILookup<int, Contact> contacts,
        IReadOnlyList<StagedMedia> staged,
        GedcomExportOptions options)
    {
        if (options.Names)
        {
            ApplyName(individual, person);
        }

        if (options.Notes)
        {
            ApplyNotes(individual.Structure, person.Note);
        }

        if (options.Events)
        {
            ApplyEvents(individual, events);
        }

        if (options.Contacts)
        {
            ApplyContacts(individual.Structure, contacts[person.Id]);
        }

        ApplyMedia(individual.Structure, staged.Where(item => item.PersonId == person.Id));
    }

    private static void ApplyName(IndividualRecord individual, Person person)
    {
        var name = individual.PrimaryName ?? individual.AddName(person.GivenNames, person.FamilyName);
        string wanted = PersonalName.FormatValue(person.GivenNames, person.FamilyName);
        var split = PersonalName.Split(name.FullValue);
        bool givenSame = (name.Structure.ChildValue("GIVN") is { } given ? GivenNamesFrom(given) : split.Given) == person.GivenNames;
        bool surnameSame = string.Equals((name.Structure.ChildValue("SURN") ?? split.Surname)?.Trim(), person.FamilyName, StringComparison.Ordinal);
        if (!givenSame || !surnameSame)
        {
            name.FullValue = wanted;
            name.Given = person.GivenNames;
            name.Surname = person.FamilyName;
        }

        name.Prefix = person.NamePrefix;
        name.Nickname = person.Nickname;
        name.Suffix = person.NameSuffix;
        individual.Sex = person.IsMale switch
        {
            true => GedcomSex.Male,
            false => GedcomSex.Female,
            _ => GedcomSex.Unknown,
        };
    }

    private static void ApplyNotes(GedcomStructure owner, string? note)
    {
        string wanted = note?.Replace("\r\n", "\n").Trim() ?? string.Empty;
        string current = string.Join("\n\n", owner.ChildrenWithTag("NOTE", "SNOTE").Select(child => new NoteLink(child).Text?.Trim()).Where(text => !string.IsNullOrWhiteSpace(text)));
        if (string.Equals(current, wanted, StringComparison.Ordinal))
        {
            return;
        }

        owner.RemoveChildren("NOTE");
        owner.RemoveChildren("SNOTE");
        if (wanted.Length > 0)
        {
            owner.AddChild("NOTE", wanted);
        }
    }

    private static void ApplyEvents(IndividualRecord individual, List<PersonEvent> events)
    {
        var timeline = events.Where(evt => !PersonEventType.IsNonTimelineEventType(TagOf(evt))).ToList();
        var consumed = new HashSet<int>();
        foreach (var child in individual.Structure.Children.ToList())
        {
            if (!IsManagedEvent(child, timeline) || IsContactOnly(new GedcomEvent(child)))
            {
                continue;
            }

            var match = timeline.FirstOrDefault(evt => !consumed.Contains(evt.Id) && TagOf(evt).Equals(child.Tag, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                child.Remove();
                continue;
            }

            consumed.Add(match.Id);
            UpdateEvent(child, match);
        }

        foreach (var evt in timeline.Where(evt => !consumed.Contains(evt.Id)))
        {
            string tag = TagOf(evt);
            PersonEventType.TryReadStoredTag(evt.EventType, evt.Description, out _, out string? detail);
            var added = individual.Structure.AddChild(tag, valueOnLineTags.Contains(tag) ? detail : null);
            if (!valueOnLineTags.Contains(tag))
            {
                added.SetChildValue("TYPE", detail);
            }

            UpdateEvent(added, evt);
        }
    }

    private static bool IsManagedEvent(GedcomStructure child, List<PersonEvent> events) =>
        managedEventTags.Contains(child.Tag)
        || events.Any(evt => TagOf(evt).Equals(child.Tag, StringComparison.OrdinalIgnoreCase))
        || (child.IsExtension && (child.Child("DATE") is not null || child.Child("PLAC") is not null));

    private static void UpdateEvent(GedcomStructure structure, PersonEvent evt)
    {
        PersonEventType.TryReadStoredTag(evt.EventType, evt.Description, out string tag, out string? detail);
        if (valueOnLineTags.Contains(tag))
        {
            structure.Value = string.IsNullOrWhiteSpace(detail) ? null : detail;
        }
        else
        {
            structure.SetChildValue("TYPE", string.IsNullOrWhiteSpace(detail) ? null : detail);
        }

        var currentDate = ParseDateString(structure.ChildValue("DATE"));
        if (!SameDate(currentDate, evt.Year, evt.Month, evt.Day))
        {
            structure.SetChildValue("DATE", evt.Year is null ? null : FormatDate(evt.Year, evt.Month, evt.Day));
        }

        var view = new GedcomEvent(structure);
        string place = evt.Place?.Trim() ?? string.Empty;
        if (!string.Equals(view.PlaceName?.Trim() ?? string.Empty, place, StringComparison.Ordinal))
        {
            view.PlaceName = place.Length == 0 ? null : place;
        }

        if (evt.Latitude is double latitude && evt.Longitude is double longitude && view.Place?.Latitude is null)
        {
            var coordinates = view.Place ?? new Place(structure.GetOrAddChild("PLAC"));
            coordinates.Latitude = latitude;
            coordinates.Longitude = longitude;
        }

        ApplyNotes(structure, evt.Note);
    }

    private static void ApplyContacts(GedcomStructure individual, IEnumerable<Contact> contacts)
    {
        foreach (string tag in contactTags)
        {
            individual.RemoveChildren(tag);
        }

        foreach (var contact in contacts.OrderBy(contact => contact.SortOrder))
        {
            if (string.IsNullOrWhiteSpace(contact.Value))
            {
                continue;
            }

            individual.AddChild(ContactTypes.GedcomTag(contact.Type), SingleLine(contact.Value));
        }
    }

    private static void ApplyMedia(GedcomStructure individual, IEnumerable<StagedMedia> items)
    {
        individual.RemoveChildren("OBJE");
        foreach (var item in items)
        {
            var link = individual.AddChild("OBJE");
            var file = link.AddChild("FILE", item.ZipPath);
            if (!string.IsNullOrWhiteSpace(item.Form))
            {
                file.AddChild("FORM", item.Form);
            }

            if (!string.IsNullOrWhiteSpace(item.Title))
            {
                file.AddChild("TITL", SingleLine(item.Title));
            }

            if (!string.IsNullOrWhiteSpace(item.Date))
            {
                file.AddChild("_DATE", SingleLine(item.Date));
            }

            if (!string.IsNullOrWhiteSpace(item.Text))
            {
                file.AddChild("_TEXT", item.Text);
            }
        }
    }

    private static void ApplyFamilies(GedcomSharp.GedcomFile dataset, List<Person> people, List<PersonSpouse> spouses)
    {
        var kept = new HashSet<GedcomStructure>();
        foreach (var spouse in spouses)
        {
            var first = people.FirstOrDefault(person => person.Id == spouse.PersonId);
            var second = people.FirstOrDefault(person => person.Id == spouse.SpouseId);
            var husband = first?.IsMale == true ? first : second?.IsMale == true ? second : first;
            var wife = husband == first ? second : first;
            if (husband is null && wife is null)
            {
                continue;
            }

            string? husbandXref = husband is null ? null : XrefKey(FormatXref(husband.GedcomId, husband.Id));
            string? wifeXref = wife is null ? null : XrefKey(FormatXref(wife.GedcomId, wife.Id));
            var family = dataset.Families.FirstOrDefault(candidate =>
                SameXref(candidate.HusbandXref, husbandXref) && SameXref(candidate.WifeXref, wifeXref));
            if (family is null)
            {
                family = dataset.AddFamily(dataset.Find<IndividualRecord>(husbandXref), dataset.Find<IndividualRecord>(wifeXref));
            }

            kept.Add(family.Structure);
            SetFamilyDate(family, "MARR", spouse.MarriageYear, spouse.MarriageMonth, spouse.MarriageDay);
            SetFamilyDate(family, "DIV", spouse.DivorceYear, spouse.DivorceMonth, spouse.DivorceDay);
            if (spouse.HasEngagement || spouse.EngagementYear is not null)
            {
                SetFamilyDate(family, "ENGA", spouse.EngagementYear, spouse.EngagementMonth, spouse.EngagementDay);
            }

            var childXrefs = people
                .Where(person => husband is not null && wife is not null && ((person.FatherId == husband.Id && person.MotherId == wife.Id) || (person.FatherId == wife.Id && person.MotherId == husband.Id)))
                .Select(person => XrefKey(FormatXref(person.GedcomId, person.Id)))
                .ToList();
            foreach (var link in family.Structure.ChildrenWithTag("CHIL").Where(link => !childXrefs.Any(xref => SameXref(link.Pointer, xref))).ToList())
            {
                link.Remove();
            }

            foreach (string childXref in childXrefs)
            {
                var child = dataset.Find<IndividualRecord>(childXref);
                if (child is not null && !family.ChildXrefs.Any(xref => SameXref(xref, childXref)))
                {
                    dataset.AddChildToFamily(family, child);
                }
            }
        }

        foreach (var family in dataset.Families.Where(family => !kept.Contains(family.Structure)).ToList())
        {
            dataset.Remove(family.Structure);
        }
    }

    private static void SetFamilyDate(FamilyRecord family, string tag, short? year, byte? month, byte? day)
    {
        if (year is null)
        {
            return;
        }

        var existing = family.GetEvent(tag);
        if (existing is not null && SameDate(ParseDateString(existing.DateText), year, month, day))
        {
            return;
        }

        var added = existing ?? family.AddEvent(tag);
        added.Structure.SetChildValue("DATE", FormatDate(year, month, day));
    }

    private async Task WriteExportAsync(GedcomSharp.GedcomFile dataset, GedcomExportOptions options, IReadOnlyList<StagedMedia> staged, string directory, CancellationToken cancellationToken)
    {
        var writer = Writer(options, dataset);
        if (staged.Count == 0)
        {
            var bytes = writer.WriteToBytes(dataset);
            if (!bytes.IsSuccess)
            {
                throw new InvalidOperationException(string.Join(" ", bytes.ValidationErrors.Select(error => error.ErrorMessage)));
            }

            await File.WriteAllBytesAsync(Path.Combine(directory, "kinnect.ged"), bytes.Value, cancellationToken);
            return;
        }

        await using var stream = File.Create(Path.Combine(directory, "kinnect.gdz"));
        var entries = staged.DistinctBy(item => item.ZipPath).Select(item => GedzipFileEntry.FromPath(item.ZipPath, item.SourcePath));
        var written = await writer.WriteGedzipAsync(dataset, stream, entries, cancellationToken);
        if (!written.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(" ", written.ValidationErrors.Select(error => error.ErrorMessage)));
        }
    }

    private static GedcomSharp.GedcomWriter Writer(GedcomExportOptions options, GedcomSharp.GedcomFile dataset) =>
        new(new GedcomSharp.GedcomWriterOptions
        {
            Version = ParseExportVersion(options.Version),
            SourceSystem = dataset.Source is null ? "Kinnect" : null,
        });

    private static GedcomVersion ParseExportVersion(string? value) => value switch
    {
        "5.5" => GedcomVersion.V55,
        "5.5.5" => GedcomVersion.V555,
        "7.0" => GedcomVersion.V70,
        _ => GedcomVersion.V551,
    };

    private async Task<GedcomSharp.GedcomFile?> ReadStoredDatasetAsync(CancellationToken cancellationToken)
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
        var read = new GedcomSharp.GedcomReader().Read(bytes, file.FileName);
        if (!read.IsSuccess)
        {
            logger.LogWarning("The stored GEDCOM could not be read. A new file was written instead.");
            return null;
        }

        return read.Value;
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

    private static string SingleLine(string value) =>
        value.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
}
