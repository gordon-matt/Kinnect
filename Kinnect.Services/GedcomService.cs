using GedcomSharp.Model;
using GedcomTags = GedcomSharp.GedcomTags;
using Kinnect.Models.FamilyTree;
using GedcomDate = GedcomSharp.Values.GedcomDate;
using GedcomSex = GedcomSharp.Values.GedcomSex;

namespace Kinnect.Services;

public partial class GedcomService(
    IRepository<Person> personRepository,
    IRepository<PersonSpouse> spouseRepository,
    IRepository<PersonEvent> eventRepository,
    IRepository<Photo> photoRepository,
    IRepository<PersonPhoto> personPhotoRepository,
    IRepository<PhotoEvent> photoEventRepository,
    IRepository<Document> documentRepository,
    IRepository<Video> videoRepository,
    IRepository<Contact> contactRepository,
    IRepository<GedcomFile> gedcomFileRepository,
    IFileStorageService fileStorage,
    ILogger<GedcomService> logger) : IGedcomService
{
    public Task<string> ExportAsync() =>
        BuildGedcomAsync(GedcomExportOptions.WithoutMedia(), progress: null, staged: [], CancellationToken.None);

    public async Task<GedcomExportFile> ExportFileAsync(
        GedcomExportOptions options,
        IProgress<GedcomExportProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var staged = await StageMediaAsync(options, cancellationToken);
        var dataset = await BuildDatasetAsync(options, progress, staged, cancellationToken);
        string directory = Directory.CreateTempSubdirectory("kinnect-gedcom-export-").FullName;
        var file = await WriteExportAsync(dataset, options, staged, directory, cancellationToken);
        progress?.Report(new GedcomExportProgress(1, 1, file.DownloadName));
        return file;
    }

    private async Task<List<StagedMedia>> StageMediaAsync(GedcomExportOptions options, CancellationToken cancellationToken)
    {
        var staged = new List<StagedMedia>();
        if (options.Photos)
        {
            var photos = (await photoRepository.FindAsync(new SearchOptions<Photo>())).ToList();
            var links = (await personPhotoRepository.FindAsync(new SearchOptions<PersonPhoto>())).ToList();
            foreach (var photo in photos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var people = links.Where(link => link.PhotoId == photo.Id).Select(link => link.PersonId).Distinct().ToList();
                if (people.Count == 0)
                {
                    people.Add(photo.UploadedByPersonId);
                }

                AddMedia(staged, people, photo.Id, photo.FilePath, photo.Title, photo.Description, photo.YearTaken, photo.MonthTaken, photo.DayTaken);
            }
        }

        if (options.Videos)
        {
            foreach (var video in await videoRepository.FindAsync(new SearchOptions<Video>()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddMedia(staged, [video.UploadedByPersonId], video.Id, video.FilePath, video.Title, video.Description, null, null, null);
            }
        }

        if (options.Documents)
        {
            foreach (var document in await documentRepository.FindAsync(new SearchOptions<Document>()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddMedia(staged, [document.UploadedByPersonId], document.Id, document.FilePath, document.Title, document.Description, null, null, null);
            }
        }

        return staged;
    }

    private void AddMedia(
        List<StagedMedia> staged,
        IReadOnlyList<int> personIds,
        int id,
        string relativePath,
        string? title,
        string? description,
        short? year,
        byte? month,
        byte? day)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        string source = fileStorage.GetFullPath(relativePath);
        if (!File.Exists(source))
        {
            return;
        }

        string extension = Path.GetExtension(source);
        string zipPath = $"media/{id}{extension}";
        var (date, text) = ExportCaption(description, year, month, day);
        string form = extension.TrimStart('.').ToUpperInvariant();
        foreach (int personId in personIds.Distinct())
        {
            staged.Add(new StagedMedia(personId, source, zipPath, title, form, date, text));
        }
    }


    private static (string? Date, string? Text) ExportCaption(string? description, short? year, byte? month, byte? day)
    {
        if (year is not null)
        {
            return (FormatDate(year, month, day), string.IsNullOrWhiteSpace(description) ? null : description);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return (null, null);
        }

        const string prefix = "Date: ";
        if (description.StartsWith(prefix, StringComparison.Ordinal))
        {
            int split = description.IndexOf("\n\n", StringComparison.Ordinal);
            string date = description[prefix.Length..(split < 0 ? description.Length : split)].Trim();
            string? text = split < 0 ? null : description[(split + 2)..].Trim();
            return (date, string.IsNullOrWhiteSpace(text) ? null : text);
        }

        return (null, description);
    }

    private sealed record StagedMedia(int PersonId, string SourcePath, string ZipPath, string? Title, string Form, string? Date, string? Text);

    public async Task<Result<GedcomImportResult>> ImportAsync(Stream stream, string? fileName = null)
    {
        var summary = new GedcomImportResult();
        var workDirectory = Directory.CreateTempSubdirectory("kinnect-gedcom-");
        GedcomMediaArchive? archive = null;

        try
        {
            string uploadPath = Path.Combine(workDirectory.FullName, "upload");
            await using (var fs = File.Create(uploadPath))
            {
                await stream.CopyToAsync(fs);
            }

            // A .ged only holds text. Photos arrive beside it in a .zip or .gdz, so accept either.
            string gedcomPath = Path.Combine(workDirectory.FullName, "import.ged");
            string originalName = fileName ?? "import.ged";
            GedcomSharp.GedcomFile? dataset = null;
            if (GedcomMediaArchive.IsZip(uploadPath))
            {
                archive = GedcomMediaArchive.Open(uploadPath);
                if (archive.GedcomEntryName is not null)
                {
                    archive.ExtractGedcom(gedcomPath);
                    originalName = Path.GetFileName(archive.GedcomEntryName.Replace('\\', '/'));
                }
                else if (await ReadGedcomXAsync(uploadPath) is { } packaged)
                {
                    dataset = packaged;
                    gedcomPath = uploadPath;
                }
                else
                {
                    return Result.Error("The archive does not contain a GEDCOM (.ged or .gedcom) or a GEDCOM X document.");
                }
            }
            else if (await ReadGedcomXAsync(uploadPath) is { } document)
            {
                dataset = document;
                gedcomPath = uploadPath;
            }
            else
            {
                File.Move(uploadPath, gedcomPath);
            }

            if (dataset is null)
            {
                var read = await new GedcomSharp.GedcomReader().ReadAsync(gedcomPath);
                if (!read.IsSuccess)
                {
                    string detail = string.Join(" ", read.Errors.Concat(read.ValidationErrors.Select(error => error.ErrorMessage)));
                    return Result.Error(string.IsNullOrWhiteSpace(detail) ? "The file could not be read as GEDCOM." : detail);
                }

                dataset = read.Value;
            }

            await StoreOriginalAsync(gedcomPath, originalName);

            var context = new MediaImportContext(archive, summary);

            // Map GEDCOM XRef IDs → our DB person IDs
            var xrefToPersonId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var individual in dataset.Individuals)
            {
                await ImportIndividualAsync(individual, xrefToPersonId, summary, context);
            }

            foreach (var family in dataset.Families)
            {
                await ImportFamilyAsync(family, xrefToPersonId, summary, context);
            }

            await RestoreFoldedTagsAsync(gedcomPath);
            context.AddSummaryWarnings();
            return Result.Success(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GEDCOM import failed");
            return Result.Error($"GEDCOM import failed: {ex.Message}");
        }
        finally
        {
            archive?.Dispose();
            try
            {
                workDirectory.Delete(recursive: true);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Could not remove temporary GEDCOM import folder {Folder}", workDirectory.FullName);
            }
        }
    }

    /// <summary>GEDCOM X JSON, XML, or a .gedx package. Returns null when the file is ordinary GEDCOM.</summary>
    private static async Task<GedcomSharp.GedcomFile?> ReadGedcomXAsync(string path)
    {
        try
        {
            var serializer = new GedcomSharp.GedcomX.GedcomXSerializer();
            if (GedcomMediaArchive.IsZip(path))
            {
                await using var stream = File.OpenRead(path);
                var packaged = await serializer.ReadFileAsync(stream);
                return packaged.IsSuccess && packaged.Value.MainDocument is { } document
                    ? GedcomSharp.GedcomX.GedcomXConverter.FromGedcomX(document)
                    : null;
            }

            await using var textStream = File.OpenRead(path);
            int first = textStream.ReadByte();
            if (first is not ('{' or '<'))
            {
                return null;
            }

            textStream.Position = 0;
            using var reader = new StreamReader(textStream);
            string text = await reader.ReadToEndAsync();
            var parsed = first == '{' ? serializer.FromJson(text) : serializer.FromXml(text);
            return parsed.IsSuccess ? GedcomSharp.GedcomX.GedcomXConverter.FromGedcomX(parsed.Value) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void ApplyFacts(IndividualRecord individual, Person person)
    {
        person.Occupation = FactValue(FindFact(individual, GedcomTags.OCCU)) ?? person.Occupation;
        person.Education = FactValue(FindFact(individual, GedcomTags.EDUC)) ?? person.Education;
        person.Religion = FactValue(FindFact(individual, GedcomTags.RELI)) ?? person.Religion;
    }

    private static GedcomEvent? FindFact(IndividualRecord individual, string tag) =>
        individual.Events.FirstOrDefault(fact => fact.Tag == tag);

    /// <summary>A fact's text is the TYPE (classification) when there is one, and otherwise the value on the tag itself.</summary>
    private static string? FactValue(GedcomEvent? fact)
    {
        if (fact is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(fact.Type))
        {
            return fact.Type;
        }

        return string.IsNullOrWhiteSpace(fact.Value) || fact.Value == "Y" ? null : fact.Value;
    }

    private static string FormatDate(short? year, byte? month, byte? day)
    {
        if (year == null)
        {
            return string.Empty;
        }

        if (month == null)
        {
            return year.Value.ToString("D4");
        }

        string monthAbbr = new DateTime(year.Value, month.Value, 1).ToString("MMM").ToUpperInvariant();
        return day == null ? $"{monthAbbr} {year:D4}" : $"{day:D2} {monthAbbr} {year:D4}";
    }

    private static (short? Year, byte? Month, byte? Day) ParseDate(GedcomDate date)
    {
        if (date.IsEmpty)
        {
            return (null, null, null);
        }

        // A parsed date keeps a partial precision (year only, or year and month). Anything that did not parse
        // is read from the original text, which still holds forms such as "ABT 1850".
        if (date.Start is { Year: > 0 } start)
        {
            return ((short)start.Year, (byte?)start.Month, (byte?)start.Day);
        }

        return ParseDateString(date.OriginalText ?? date.ToString());
    }

    private static (short? Year, byte? Month, byte? Day) ParseDateString(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return (null, null, null);
        }

        string[] monthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

        // Strip qualifiers
        foreach (string? q in new[] { "ABT", "ABOUT", "EST", "BEF", "AFT", "CAL", "CIRCA", "~", "INT", "FROM", "TO", "BET", "AND" })
        {
            if (dateString.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            {
                dateString = dateString[q.Length..].Trim();
            }
        }

        short? year = null;
        byte? month = null;
        var smallNumbers = new List<byte>();
        foreach (string part in dateString.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (short.TryParse(part, out short number) && number is >= 1 and <= 9999)
            {
                // A day is at most 31. A longer number, or anything above 31, is the year.
                if (number > 31 || part.Length >= 3)
                {
                    year = number;
                }
                else
                {
                    smallNumbers.Add((byte)number);
                }
            }
            else
            {
                int idx = Array.FindIndex(monthNames, m => m.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    month = (byte)(idx + 1);
                }
            }
        }

        byte? day = null;
        if (smallNumbers.Count == 1 && (month is not null || year is not null))
        {
            day = smallNumbers[0];
        }
        else if (smallNumbers.Count == 1)
        {
            year = smallNumbers[0];
        }
        else if (smallNumbers.Count > 1)
        {
            day = smallNumbers[0];
            year ??= smallNumbers[1];
        }

        return (year, month, day);
    }

    private static string? FirstNote(IEnumerable<NoteLink> notes)
    {
        foreach (var note in notes)
        {
            if (!string.IsNullOrWhiteSpace(note.Text))
            {
                return note.Text;
            }
        }

        return null;
    }


    private async Task ImportFamilyAsync(
        FamilyRecord family,
        Dictionary<string, int> xrefToPersonId,
        GedcomImportResult summary,
        MediaImportContext context)
    {
        int? husbandId = family.HusbandXref is { } husband && xrefToPersonId.TryGetValue(husband, out int hid) ? hid : null;
        int? wifeId = family.WifeXref is { } wife && xrefToPersonId.TryGetValue(wife, out int wid) ? wid : null;

        if (husbandId.HasValue && wifeId.HasValue)
        {
            int low = Math.Min(husbandId.Value, wifeId.Value);
            int high = Math.Max(husbandId.Value, wifeId.Value);

            bool alreadyLinked = await spouseRepository.ExistsAsync(x => x.PersonId == low && x.SpouseId == high);
            if (!alreadyLinked)
            {
                var spouseRecord = new PersonSpouse { PersonId = low, SpouseId = high };

                if (family.Marriage != null)
                {
                    var (Year, Month, Day) = ParseDate(family.Marriage.Date);
                    spouseRecord.MarriageYear = Year;
                    spouseRecord.MarriageMonth = Month;
                    spouseRecord.MarriageDay = Day;
                }

                var divorceEvent = family.Events.FirstOrDefault(familyEvent => familyEvent.Tag == GedcomTags.DIV);
                var engagementEvent = family.Events.FirstOrDefault(familyEvent => familyEvent.Tag == GedcomTags.ENGA);

                if (divorceEvent != null)
                {
                    var (Year, Month, Day) = ParseDate(divorceEvent.Date);
                    spouseRecord.DivorceYear = Year;
                    spouseRecord.DivorceMonth = Month;
                    spouseRecord.DivorceDay = Day;
                }

                if (engagementEvent != null)
                {
                    var (Year, Month, Day) = ParseDate(engagementEvent.Date);
                    spouseRecord.HasEngagement = true;
                    spouseRecord.EngagementYear = Year;
                    spouseRecord.EngagementMonth = Month;
                    spouseRecord.EngagementDay = Day;
                }

                await spouseRepository.InsertAsync(spouseRecord);
                summary.RelationshipsImported++;
            }
        }

        // Link children to their parents
        foreach (string childXref in family.ChildXrefs)
        {
            if (!xrefToPersonId.TryGetValue(childXref, out int childId))
            {
                continue;
            }

            var child = await personRepository.FindOneAsync(childId);
            if (child is null)
            {
                continue;
            }

            bool updated = false;
            if (husbandId.HasValue && child.FatherId != husbandId) { child.FatherId = husbandId; updated = true; }
            if (wifeId.HasValue && child.MotherId != wifeId) { child.MotherId = wifeId; updated = true; }

            if (updated)
            {
                child.UpdatedAtUtc = DateTime.UtcNow;
                await personRepository.UpdateAsync(child);
                summary.RelationshipsImported++;
            }
        }

        await ImportFamilyMediaAsync(family, husbandId, wifeId, context);
        await ImportFamilyEventsAsync(family, husbandId, wifeId, summary);
    }

    /// <summary>
    /// Marriage, divorce and engagement dates live on the spouse link. Every other family event is copied onto
    /// both spouses, because Kinnect has no family-event table of its own.
    /// </summary>
    private async Task ImportFamilyEventsAsync(
        FamilyRecord family,
        int? husbandId,
        int? wifeId,
        GedcomImportResult summary)
    {
        var personIds = new[] { husbandId, wifeId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (personIds.Count == 0)
        {
            return;
        }

        foreach (var familyEvent in family.Events)
        {
            if (familyEvent.Tag is GedcomTags.MARR or GedcomTags.DIV or GedcomTags.ENGA)
            {
                continue;
            }

            foreach (int personId in personIds)
            {
                await InsertEventAsync(personId, familyEvent, summary, familyEvent.Tag);
            }
        }
    }

    private async Task ImportIndividualAsync(
        IndividualRecord individual,
        Dictionary<string, int> xrefToPersonId,
        GedcomImportResult summary,
        MediaImportContext context)
    {
        string xref = individual.Xref ?? string.Empty;
        var person = await personRepository.FindOneAsync(new SearchOptions<Person>
        {
            Query = x => x.GedcomId == xref
        });

        var name = individual.Names.FirstOrDefault();
        bool? isMale = individual.Sex switch
        {
            GedcomSex.Male => true,
            GedcomSex.Female => false,
            _ => null,
        };
        bool isNewPerson = false;

        if (person is null)
        {
            person = new Person
            {
                IsMale = isMale,
                GedcomId = xref,
                Note = PersonNote(individual),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            ApplyName(person, name);
            ApplyFacts(individual, person);
            await personRepository.InsertAsync(person);
            summary.PeopleImported++;
            isNewPerson = true;
        }
        else
        {
            ApplyName(person, name);
            person.IsMale = isMale;
            person.Note = PersonNote(individual) ?? person.Note;
            ApplyFacts(individual, person);
            person.UpdatedAtUtc = DateTime.UtcNow;
            await personRepository.UpdateAsync(person);
            summary.PeopleUpdated++;
        }

        xrefToPersonId[xref] = person.Id;

        var importedEvents = await ImportPersonEventsAsync(individual, person.Id, summary, replaceExisting: !isNewPerson);

        // Re-importing over an existing person replaces their events, which would orphan or duplicate photos.
        // Media is therefore only brought in for people created by this import.
        await ImportContactsAsync(individual, person.Id, replaceExisting: !isNewPerson);

        if (isNewPerson)
        {
            context.NewPersonIds.Add(person.Id);
            await ImportIndividualMediaAsync(individual, person, importedEvents, context);
        }
    }

    private static void ApplyName(Person person, PersonalName? name)
    {
        person.GivenNames = GivenNamesFrom(name?.Given);
        person.FamilyName = name?.Surname?.Trim() ?? string.Empty;
        person.NamePrefix = NamePart(name?.Prefix);
        person.Nickname = NamePart(name?.Nickname);
        person.NameSuffix = NamePart(name?.Suffix);
    }

    private static string? NamePart(string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.Length <= 100 ? trimmed : trimmed[..100];
    }

    /// <summary>
    /// GEDCOM <c>GIVN</c> separates given names with commas, so <c>John, Fitzgerald</c> is two given names.
    /// </summary>
    private static string GivenNamesFrom(string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
        {
            return "Unknown";
        }

        string spaced = System.Text.RegularExpressions.Regex.Replace(given.Trim(), @"\s*,\s*", " ");
        spaced = System.Text.RegularExpressions.Regex.Replace(spaced, @"\s+", " ").Trim();
        if (spaced.Length == 0)
        {
            return "Unknown";
        }

        return spaced.Length <= 200 ? spaced : spaced[..200];
    }

    /// <summary>The person's own notes, joined. Event notes stay on the events.</summary>
    private static string? PersonNote(IndividualRecord individual)
    {
        var notes = individual.Notes
            .Select(note => note.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        return notes.Count == 0 ? null : Truncate(string.Join("\n\n", notes), 4000);
    }

    /// <summary>
    /// True when the record is only an address. An address with no date, place or note is contact information,
    /// and importing it as an event would add a blank row.
    /// </summary>
    private static bool IsContactOnly(GedcomEvent source)
    {
        if (source.Contact.Address is null && !source.Contact.Emails.Any() && !source.Contact.Phones.Any())
        {
            return false;
        }

        var (year, month, day) = ParseDate(source.Date);
        if (year.HasValue || month.HasValue || day.HasValue || !string.IsNullOrWhiteSpace(source.PlaceName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(source.Type) || (!string.IsNullOrWhiteSpace(source.Value) && source.Value != "Y"))
        {
            return false;
        }

        return FirstNote(source.Notes) is null;
    }

    private async Task ImportContactsAsync(IndividualRecord individual, int personId, bool replaceExisting)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contacts = new List<Contact>();
        void Add(ContactType type, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string trimmed = value.Trim();
            if (!seen.Add(type + "\n" + trimmed))
            {
                return;
            }

            contacts.Add(new Contact
            {
                PersonId = personId,
                Type = type,
                Value = Truncate(trimmed, Contact.MaxValueLength),
                SortOrder = contacts.Count,
            });
        }

        void AddDetails(ContactDetails details)
        {
            foreach (string email in details.Emails)
            {
                Add(ContactType.Email, email);
            }

            foreach (string phone in details.Phones)
            {
                Add(ContactType.Phone, phone);
            }

            foreach (string fax in details.Faxes)
            {
                Add(ContactType.Fax, fax);
            }

            foreach (string website in details.Websites)
            {
                Add(ContactType.Website, website);
            }

            if (details.Address is { } address)
            {
                string postal = string.Join(", ", new[]
                {
                    address.Lines, address.Line1, address.Line2, address.Line3,
                    address.City, address.State, address.PostalCode, address.Country,
                }.Where(part => !string.IsNullOrWhiteSpace(part)).Distinct(StringComparer.OrdinalIgnoreCase));
                Add(ContactType.Address, postal);
            }
        }

        AddDetails(new ContactDetails(individual.Structure));
        foreach (var fact in PersonEvents(individual))
        {
            AddDetails(fact.Contact);
        }

        if (contacts.Count == 0)
        {
            return;
        }

        if (replaceExisting)
        {
            await contactRepository.DeleteAsync(contact => contact.PersonId == personId);
        }

        await contactRepository.InsertAsync(contacts);
    }

    private static readonly HashSet<string> skippedExtensions = new(StringComparer.Ordinal)
    {
        "_UID", "_EMAIL", "_URL", "_WWW", "_FAX", "_PRIM", "_PRIMARY", "_RIN",
    };

    /// <summary>
    /// Standard events and attributes, plus extension tags such as <c>_MILT</c> that carry a value.
    /// Identifier and contact extensions are not events.
    /// </summary>
    private static IEnumerable<GedcomEvent> PersonEvents(IndividualRecord individual)
    {
        var events = individual.Events.ToList();
        var seen = events.Select(gedcomEvent => gedcomEvent.Structure).ToHashSet();
        foreach (var child in individual.Structure.Children)
        {
            if (!child.IsExtension || skippedExtensions.Contains(child.Tag) || !seen.Add(child))
            {
                continue;
            }

            if (child.HasValue || child.HasChildren)
            {
                events.Add(new GedcomEvent(child));
            }
        }

        return events;
    }

    /// <summary>Imports the individual's events and returns, for each, the GEDCOM record it came from and its new id.</summary>
    private async Task<List<(GedcomEvent Source, int EventId)>> ImportPersonEventsAsync(
        IndividualRecord individual,
        int personId,
        GedcomImportResult summary,
        bool replaceExisting)
    {
        var imported = new List<(GedcomEvent Source, int EventId)>();

        // Clear old imported events before re-importing. A person created by this import has none.
        if (replaceExisting)
        {
            await eventRepository.DeleteAsync(x => x.PersonId == personId);
        }

        // An address with no date or place is contact information, so it is not also stored as an event.
        foreach (var indEvent in PersonEvents(individual))
        {
            if (IsContactOnly(indEvent))
            {
                continue;
            }

            var saved = await InsertEventAsync(personId, indEvent, summary, tagOverride: null);
            imported.Add((indEvent, saved.Id));
        }

        return imported;
    }

    private async Task<PersonEvent> InsertEventAsync(
        int personId,
        GedcomEvent source,
        GedcomImportResult summary,
        string? tagOverride,
        string? descriptionOverride = null)
    {
        string tag = tagOverride ?? source.Tag;
        if (string.IsNullOrWhiteSpace(tag) || tag is "_UNKN")
        {
            tag = PersonEventType.Custom;
        }

        if (tag.Length > 20)
        {
            tag = tag[..20];
        }

        var (year, month, day) = ParseDate(source.Date);
        var (latitude, longitude) = GedcomCoordinates.Read(source.Place);
        string? description = descriptionOverride;
        if (description is null)
        {
            // OCCU Clerk stores the value on the tag. EVEN / TYPE Enlisted stores it on TYPE.
            description = !string.IsNullOrWhiteSpace(source.Type)
                ? source.Type
                : string.IsNullOrWhiteSpace(source.Value) || source.Value == "Y" ? null : source.Value;
        }

        var saved = await eventRepository.InsertAsync(new PersonEvent
        {
            PersonId = personId,
            EventType = tag,
            Year = year,
            Month = month,
            Day = day,
            Place = source.PlaceName,
            Latitude = latitude,
            Longitude = longitude,
            Description = description is null ? null : Truncate(description, 2000),
            Note = FirstNote(source.Notes) is string note ? Truncate(note, 4000) : null,
            CreatedAtUtc = DateTime.UtcNow
        });

        summary.EventsImported++;
        return saved;
    }

}
