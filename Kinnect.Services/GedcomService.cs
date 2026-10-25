using System.Text;
using GeneGenie.Gedcom;
using GeneGenie.Gedcom.Enums;
using GeneGenie.Gedcom.Parser;
using Kinnect.Models.FamilyTree;

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
    public async Task<string> ExportAsync()
    {
        var people = (await personRepository.FindAsync(new SearchOptions<Person>())).ToList();
        var spouses = (await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>())).ToList();
        var events = (await eventRepository.FindAsync(new SearchOptions<PersonEvent>())).ToList();

        var eventsByPerson = events.ToLookup(e => e.PersonId);
        var contactsByPerson = (await contactRepository.FindAsync(new SearchOptions<Contact>())).ToLookup(c => c.PersonId);
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("0 HEAD");
        sb.AppendLine("1 SOUR Kinnect");
        sb.AppendLine("1 GEDC");
        sb.AppendLine("2 VERS 5.5.1");
        sb.AppendLine("1 CHAR UTF-8");
        sb.AppendLine($"1 DATE {DateTime.UtcNow:dd MMM yyyy}".ToUpperInvariant());

        // Individuals
        foreach (var person in people)
        {
            string xref = person.GedcomId ?? $"@I{person.Id}@";
            sb.AppendLine($"0 {xref} INDI");
            sb.AppendLine($"1 NAME {person.GivenNames} /{person.FamilyName}/");
            sb.AppendLine($"1 SEX {person.IsMale switch { true => "M", false => "F", _ => "U" }}");

            var birthEvt = eventsByPerson[person.Id].FirstOrDefault(e => e.EventType == PersonEventType.Birth);
            if (birthEvt != null)
            {
                sb.AppendLine("1 BIRT");
                if (birthEvt.Year.HasValue)
                {
                    sb.AppendLine($"2 DATE {FormatDate(birthEvt.Year, birthEvt.Month, birthEvt.Day)}");
                }

                WritePlace(sb, birthEvt.Place, birthEvt.Latitude, birthEvt.Longitude);
            }

            var deathEvt = eventsByPerson[person.Id].FirstOrDefault(e => e.EventType == PersonEventType.Death);
            if (deathEvt != null)
            {
                sb.AppendLine("1 DEAT");
                if (deathEvt.Year.HasValue)
                {
                    sb.AppendLine($"2 DATE {FormatDate(deathEvt.Year, deathEvt.Month, deathEvt.Day)}");
                }

                WritePlace(sb, deathEvt.Place, deathEvt.Latitude, deathEvt.Longitude);
            }

            // These three are also stored as events. Write the profile field only when there is no event,
            // so a person who was typed in by hand still exports, and an imported fact is not listed twice.
            if (!eventsByPerson[person.Id].Any(e => e.EventType == PersonEventType.Occupation)
                && !string.IsNullOrWhiteSpace(person.Occupation))
            {
                sb.AppendLine($"1 OCCU {person.Occupation}");
            }

            if (!eventsByPerson[person.Id].Any(e => e.EventType == PersonEventType.Education)
                && !string.IsNullOrWhiteSpace(person.Education))
            {
                sb.AppendLine($"1 EDUC {person.Education}");
            }

            if (!eventsByPerson[person.Id].Any(e => e.EventType == PersonEventType.Religion)
                && !string.IsNullOrWhiteSpace(person.Religion))
            {
                sb.AppendLine($"1 RELI {person.Religion}");
            }

            if (!string.IsNullOrWhiteSpace(person.Note))
            {
                WriteNote(sb, 1, person.Note);
            }

            foreach (var contact in contactsByPerson[person.Id].OrderBy(contact => contact.SortOrder))
            {
                if (string.IsNullOrWhiteSpace(contact.Value))
                {
                    continue;
                }

                string value = contact.Value.Replace("\r\n", ", ").Replace('\n', ' ').Replace('\r', ' ').Trim();
                sb.AppendLine($"1 {ContactTypes.GedcomTag(contact.Type)} {value}");
            }

            // Additional life events (skip birth/death and types stored elsewhere)
            foreach (var evt in eventsByPerson[person.Id]
                .Where(e => e.EventType != PersonEventType.Birth && e.EventType != PersonEventType.Death
                    && !PersonEventType.IsNonTimelineEventType(e.EventType)))
            {
                sb.AppendLine($"1 {evt.EventType}");
                if (evt.Year.HasValue)
                {
                    sb.AppendLine($"2 DATE {FormatDate(evt.Year, evt.Month, evt.Day)}");
                }

                WritePlace(sb, evt.Place, evt.Latitude, evt.Longitude);

                if (!string.IsNullOrWhiteSpace(evt.Description))
                {
                    sb.AppendLine($"2 TYPE {evt.Description}");
                }

                if (!string.IsNullOrWhiteSpace(evt.Note))
                {
                    WriteNote(sb, 2, evt.Note);
                }
            }
        }

        // Families
        int familyIndex = 1;
        foreach (var spouse in spouses)
        {
            var p1 = people.FirstOrDefault(p => p.Id == spouse.PersonId);
            var p2 = people.FirstOrDefault(p => p.Id == spouse.SpouseId);

            // Identify husband/wife by gender
            var husband = (p1?.IsMale == true) ? p1 : (p2?.IsMale == true ? p2 : p1);
            var wife = husband == p1 ? p2 : p1;

            sb.AppendLine($"0 @F{familyIndex++}@ FAM");
            if (husband != null)
            {
                sb.AppendLine($"1 HUSB {husband.GedcomId ?? $"@I{husband.Id}@"}");
            }

            if (wife != null)
            {
                sb.AppendLine($"1 WIFE {wife.GedcomId ?? $"@I{wife.Id}@"}");
            }

            if (spouse.MarriageYear.HasValue)
            {
                sb.AppendLine("1 MARR");
                sb.AppendLine($"2 DATE {FormatDate(spouse.MarriageYear, spouse.MarriageMonth, spouse.MarriageDay)}");
            }

            if (spouse.DivorceYear.HasValue)
            {
                sb.AppendLine("1 DIV");
                sb.AppendLine($"2 DATE {FormatDate(spouse.DivorceYear, spouse.DivorceMonth, spouse.DivorceDay)}");
            }

            if (spouse.HasEngagement)
            {
                sb.AppendLine("1 ENGA");
                if (spouse.EngagementYear.HasValue)
                {
                    sb.AppendLine($"2 DATE {FormatDate(spouse.EngagementYear, spouse.EngagementMonth, spouse.EngagementDay)}");
                }
            }

            // Children whose both parent IDs match either spouse
            var children = people.Where(p =>
                (p.FatherId == spouse.PersonId || p.FatherId == spouse.SpouseId) &&
                (p.MotherId == spouse.PersonId || p.MotherId == spouse.SpouseId));

            foreach (var child in children)
            {
                sb.AppendLine($"1 CHIL {child.GedcomId ?? $"@I{child.Id}@"}");
            }
        }

        sb.AppendLine("0 TRLR");
        return sb.ToString();
    }

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

            // A .ged only holds text. Photos arrive in a zip next to it, so accept either.
            string gedcomPath = Path.Combine(workDirectory.FullName, "import.ged");
            string originalName = fileName ?? "import.ged";
            if (GedcomMediaArchive.IsZip(uploadPath))
            {
                archive = GedcomMediaArchive.Open(uploadPath);
                if (archive.GedcomEntryName is null)
                {
                    return Result.Error("The zip file does not contain a .ged or .gedcom file.");
                }

                archive.ExtractGedcom(gedcomPath);
                originalName = Path.GetFileName(archive.GedcomEntryName.Replace('\\', '/'));
            }
            else
            {
                File.Move(uploadPath, gedcomPath);
            }

            var reader = GedcomRecordReader.CreateReader(gedcomPath);
            var database = reader.Database;

            await StoreOriginalAsync(gedcomPath, originalName);

            var context = new MediaImportContext(archive, summary);

            // Map GEDCOM XRef IDs → our DB person IDs
            var xrefToPersonId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var individual in database.Individuals)
            {
                await ImportIndividualAsync(individual, database, xrefToPersonId, summary, context);
            }

            foreach (var family in database.Families)
            {
                await ImportFamilyAsync(family, database, xrefToPersonId, summary, context);
            }

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

    private static void ApplyFacts(GedcomIndividualRecord individual, Person person)
    {
        // Facts live on Attributes. FindEvent only walks Events, so occupation would be missed.
        person.Occupation = FactValue(FindFact(individual, GedcomEventType.OCCUFact)) ?? person.Occupation;
        person.Education = FactValue(FindFact(individual, GedcomEventType.EDUCFact)) ?? person.Education;
        person.Religion = FactValue(FindFact(individual, GedcomEventType.RELIFact)) ?? person.Religion;
    }

    private static GedcomEvent? FindFact(GedcomIndividualRecord individual, GedcomEventType type) =>
        individual.Attributes.FirstOrDefault(fact => fact.EventType == type)
        ?? individual.Events.FirstOrDefault(fact => fact.EventType == type);

    /// <summary>A fact's text is the TYPE (classification) when there is one, and otherwise the value on the tag itself.</summary>
    private static string? FactValue(GedcomEvent? fact)
    {
        if (fact is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(fact.Classification))
        {
            return fact.Classification;
        }

        return string.IsNullOrWhiteSpace(fact.EventName) ? null : fact.EventName;
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

    private static (short? Year, byte? Month, byte? Day) ParseDate(GedcomDate? date)
    {
        if (date?.DateTime1 == null && string.IsNullOrWhiteSpace(date?.DateString))
        {
            return (null, null, null);
        }

        // Prefer the parsed DateTime if available
        if (date!.DateTime1.HasValue)
        {
            var dt = date.DateTime1.Value;
            return ((short)dt.Year, (byte)dt.Month, (byte)dt.Day);
        }

        // Fall back to parsing the date string ourselves
        return ParseDateString(date.DateString);
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

        short? year = null; byte? month = null; byte? day = null;
        foreach (string part in dateString.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (short.TryParse(part, out short y) && y >= 1 && y <= 9999)
            {
                year = y;
            }
            else if (byte.TryParse(part, out byte d) && d >= 1 && d <= 31)
            {
                day = d;
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
        return (year, month, day);
    }

    private static string? ResolveFirstNote(GedcomRecordList<string> noteXrefs, GedcomDatabase db)
    {
        foreach (string xref in noteXrefs)
        {
            if (db[xref] is GedcomNoteRecord note && !string.IsNullOrWhiteSpace(note.Text))
            {
                return note.Text;
            }
        }
        return null;
    }

    // ── Export ────────────────────────────────────────────────────────────────
    // ── Helpers ───────────────────────────────────────────────────────────────
    private static void WriteNote(StringBuilder sb, int level, string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        sb.AppendLine($"{level} NOTE {lines[0]}");
        foreach (string? line in lines.Skip(1))
        {
            sb.AppendLine($"{level + 1} CONT {line}");
        }
    }

    private async Task ImportFamilyAsync(
        GedcomFamilyRecord family,
        GedcomDatabase database,
        Dictionary<string, int> xrefToPersonId,
        GedcomImportResult summary,
        MediaImportContext context)
    {
        int? husbandId = family.Husband != null && xrefToPersonId.TryGetValue(family.Husband, out int hid) ? hid : null;
        int? wifeId = family.Wife != null && xrefToPersonId.TryGetValue(family.Wife, out int wid) ? wid : null;

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

                GedcomFamilyEvent? divorceEvent = null;
                GedcomFamilyEvent? engagementEvent = null;
                foreach (var famEv in family.Events)
                {
                    if (famEv.EventType == GedcomEventType.DIV && divorceEvent is null)
                    {
                        divorceEvent = famEv;
                    }
                    else if (famEv.EventType == GedcomEventType.ENGA && engagementEvent is null)
                    {
                        engagementEvent = famEv;
                    }
                }

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
        foreach (string childXref in family.Children)
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

        await ImportFamilyMediaAsync(family, database, husbandId, wifeId, context);
        await ImportFamilyEventsAsync(family, database, husbandId, wifeId, summary);
    }

    /// <summary>
    /// Marriage, divorce and engagement dates live on the spouse link. Every other family event is copied onto
    /// both spouses, because Kinnect has no family-event table of its own.
    /// </summary>
    private async Task ImportFamilyEventsAsync(
        GedcomFamilyRecord family,
        GedcomDatabase database,
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
            string tag = GedcomEvent.TypeToTag(familyEvent.EventType);
            if (tag is PersonEventType.Marriage or PersonEventType.Divorce or PersonEventType.Engagement)
            {
                continue;
            }

            foreach (int personId in personIds)
            {
                await InsertEventAsync(personId, familyEvent, database, summary, tag);
            }
        }
    }

    private async Task ImportIndividualAsync(
        GedcomIndividualRecord individual,
        GedcomDatabase database,
        Dictionary<string, int> xrefToPersonId,
        GedcomImportResult summary,
        MediaImportContext context)
    {
        var person = await personRepository.FindOneAsync(new SearchOptions<Person>
        {
            Query = x => x.GedcomId == individual.XRefID
        });

        var name = individual.Names.FirstOrDefault();
        string givenNames = GivenNamesFrom(name?.Given);
        string familyName = name?.Surname?.Trim() ?? string.Empty;
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
                GivenNames = givenNames,
                FamilyName = familyName,
                IsMale = isMale,
                GedcomId = individual.XRefID,
                Note = PersonNote(individual, database),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            ApplyFacts(individual, person);
            await personRepository.InsertAsync(person);
            summary.PeopleImported++;
            isNewPerson = true;
        }
        else
        {
            person.GivenNames = givenNames;
            person.FamilyName = familyName;
            person.IsMale = isMale;
            person.Note = PersonNote(individual, database) ?? person.Note;
            ApplyFacts(individual, person);
            person.UpdatedAtUtc = DateTime.UtcNow;
            await personRepository.UpdateAsync(person);
            summary.PeopleUpdated++;
        }

        xrefToPersonId[individual.XRefID] = person.Id;

        var importedEvents = await ImportPersonEventsAsync(individual, database, person.Id, summary, replaceExisting: !isNewPerson);

        // Re-importing over an existing person replaces their events, which would orphan or duplicate photos.
        // Media is therefore only brought in for people created by this import.
        await ImportContactsAsync(individual, person.Id, replaceExisting: !isNewPerson);

        if (isNewPerson)
        {
            context.NewPersonIds.Add(person.Id);
            await ImportIndividualMediaAsync(individual, database, person, importedEvents, context);
        }
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
    private static string? PersonNote(GedcomIndividualRecord individual, GedcomDatabase database)
    {
        var notes = individual.Notes
            .Select(xref => database[xref] is GedcomNoteRecord note ? note.Text : null)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        return notes.Count == 0 ? null : Truncate(string.Join("\n\n", notes), 4000);
    }

    /// <summary>
    /// True when the record is only an address. The reader moves an individual's ADDR onto a residence
    /// and then clears it, so email and phone have to be read from that event.
    /// </summary>
    private static bool IsContactOnly(GedcomEvent source, GedcomDatabase database)
    {
        if (source.Address is null)
        {
            return false;
        }

        var (year, month, day) = ParseDate(source.Date);
        if (year.HasValue || month.HasValue || day.HasValue || !string.IsNullOrWhiteSpace(source.Place?.Name))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(source.Classification) || !string.IsNullOrWhiteSpace(source.EventName))
        {
            return false;
        }

        return ResolveFirstNote(source.Notes, database) is null;
    }

    private async Task ImportContactsAsync(GedcomIndividualRecord individual, int personId, bool replaceExisting)
    {
        var addresses = new List<GedcomAddress>();
        if (individual.Address is not null)
        {
            addresses.Add(individual.Address);
        }

        foreach (var fact in individual.Events.Concat(individual.Attributes))
        {
            if (fact.Address is not null)
            {
                addresses.Add(fact.Address);
            }
        }

        if (addresses.Count == 0)
        {
            return;
        }

        var contacts = new List<Contact>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        foreach (var address in addresses)
        {
            Add(ContactType.Email, address.Email1);
            Add(ContactType.Email, address.Email2);
            Add(ContactType.Email, address.Email3);
            Add(ContactType.Phone, address.Phone1);
            Add(ContactType.Phone, address.Phone2);
            Add(ContactType.Phone, address.Phone3);
            Add(ContactType.Fax, address.Fax1);
            Add(ContactType.Fax, address.Fax2);
            Add(ContactType.Fax, address.Fax3);
            Add(ContactType.Website, address.Www1);
            Add(ContactType.Website, address.Www2);
            Add(ContactType.Website, address.Www3);

            string postal = string.Join(", ", new[]
            {
                address.AddressLine, address.AddressLine1, address.AddressLine2, address.AddressLine3,
                address.City, address.State, address.PostCode, address.Country,
            }.Where(part => !string.IsNullOrWhiteSpace(part)).Distinct(StringComparer.OrdinalIgnoreCase));
            Add(ContactType.Address, postal);
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

    /// <summary>Imports the individual's events and returns, for each, the GEDCOM record it came from and its new id.</summary>
    private async Task<List<(GedcomEvent Source, int EventId)>> ImportPersonEventsAsync(
        GedcomIndividualRecord individual,
        GedcomDatabase database,
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

        // Facts such as occupation are kept on Attributes, not Events, so both lists are imported.
        // An address with no date or place is contact information. The reader also parks the
        // individual's own address on a residence, so that row would otherwise be a blank event.
        foreach (var indEvent in individual.Events.Concat(individual.Attributes))
        {
            if (IsContactOnly(indEvent, database))
            {
                continue;
            }

            var saved = await InsertEventAsync(personId, indEvent, database, summary, tagOverride: null);
            imported.Add((indEvent, saved.Id));
        }

        foreach (var custom in individual.Custom)
        {
            string description = string.IsNullOrWhiteSpace(custom.Classification)
                ? custom.Tag
                : $"{custom.Tag}: {custom.Classification}";
            var saved = await InsertEventAsync(
                personId, custom, database, summary, PersonEventType.Custom, description);
            imported.Add((custom, saved.Id));
        }

        bool hasBirtInEvents = individual.Events.Any(e => e.EventType == GedcomEventType.Birth);
        if (!hasBirtInEvents && individual.Birth != null)
        {
            var (year, month, day) = ParseDate(individual.Birth.Date);
            var (birthLatitude, birthLongitude) = GedcomCoordinates.Read(individual.Birth.Place);
            var saved = await eventRepository.InsertAsync(new PersonEvent
            {
                PersonId = personId,
                EventType = PersonEventType.Birth,
                Year = year,
                Month = month,
                Day = day,
                Place = individual.Birth.Place?.Name,
                Latitude = birthLatitude,
                Longitude = birthLongitude,
                CreatedAtUtc = DateTime.UtcNow
            });

            imported.Add((individual.Birth, saved.Id));
            summary.EventsImported++;
        }

        bool hasDeatInEvents = individual.Events.Any(e => e.EventType == GedcomEventType.DEAT);
        if (!hasDeatInEvents && individual.Death != null)
        {
            var (year, month, day) = ParseDate(individual.Death.Date);
            var (deathLatitude, deathLongitude) = GedcomCoordinates.Read(individual.Death.Place);
            var saved = await eventRepository.InsertAsync(new PersonEvent
            {
                PersonId = personId,
                EventType = PersonEventType.Death,
                Year = year,
                Month = month,
                Day = day,
                Place = individual.Death.Place?.Name,
                Latitude = deathLatitude,
                Longitude = deathLongitude,
                CreatedAtUtc = DateTime.UtcNow
            });

            imported.Add((individual.Death, saved.Id));
            summary.EventsImported++;
        }

        return imported;
    }

    private async Task<PersonEvent> InsertEventAsync(
        int personId,
        GedcomEvent source,
        GedcomDatabase database,
        GedcomImportResult summary,
        string? tagOverride,
        string? descriptionOverride = null)
    {
        string tag = tagOverride ?? GedcomEvent.TypeToTag(source.EventType);
        if (tag is "_UNKN" or "EVEN")
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
            // OCCU Clerk stores the value on EventName. EVEN / TYPE Enlisted stores it on Classification.
            description = !string.IsNullOrWhiteSpace(source.Classification)
                ? source.Classification
                : string.IsNullOrWhiteSpace(source.EventName) ? null : source.EventName;
        }

        var saved = await eventRepository.InsertAsync(new PersonEvent
        {
            PersonId = personId,
            EventType = tag,
            Year = year,
            Month = month,
            Day = day,
            Place = source.Place?.Name,
            Latitude = latitude,
            Longitude = longitude,
            Description = description is null ? null : Truncate(description, 2000),
            Note = ResolveFirstNote(source.Notes, database) is string note ? Truncate(note, 4000) : null,
            CreatedAtUtc = DateTime.UtcNow
        });

        summary.EventsImported++;
        return saved;
    }

    private static void WritePlace(StringBuilder sb, string? place, double? latitude, double? longitude)
    {
        if (string.IsNullOrWhiteSpace(place) && (latitude is null || longitude is null))
        {
            return;
        }

        sb.AppendLine(string.IsNullOrWhiteSpace(place) ? "2 PLAC" : $"2 PLAC {place}");
        if (latitude is double lat && longitude is double lng)
        {
            sb.AppendLine("3 MAP");
            sb.AppendLine($"4 LATI {GedcomCoordinates.FormatLatitude(lat)}");
            sb.AppendLine($"4 LONG {GedcomCoordinates.FormatLongitude(lng)}");
        }
    }
}