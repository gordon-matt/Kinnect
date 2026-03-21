using System.Text;
using Ardalis.Result;
using GeneGenie.Gedcom;
using GeneGenie.Gedcom.Enums;
using GeneGenie.Gedcom.Parser;
using Kinnect.Models;
using Kinnect.Services.Abstractions;

namespace Kinnect.Services;

public class GedcomService(
    IRepository<Person> personRepository,
    IRepository<PersonSpouse> spouseRepository,
    IRepository<PersonEvent> eventRepository) : IGedcomService
{
    public async Task<Result<GedcomImportResult>> ImportAsync(Stream stream)
    {
        var summary = new GedcomImportResult();
        string tempFile = Path.GetTempFileName() + ".ged";

        try
        {
            await using (var fs = File.Create(tempFile))
                await stream.CopyToAsync(fs);

            var reader = GedcomRecordReader.CreateReader(tempFile);
            var database = reader.Database;

            // Map GEDCOM XRef IDs → our DB person IDs
            var xrefToPersonId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var individual in database.Individuals)
                await ImportIndividualAsync(individual, database, xrefToPersonId, summary);

            foreach (var family in database.Families)
                await ImportFamilyAsync(family, database, xrefToPersonId, summary);

            return Result.Success(summary);
        }
        catch (Exception ex)
        {
            return Result.Error($"GEDCOM import failed: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private async Task ImportIndividualAsync(
        GedcomIndividualRecord individual,
        GedcomDatabase database,
        Dictionary<string, int> xrefToPersonId,
        GedcomImportResult summary)
    {
        var existing = await personRepository.FindAsync(new SearchOptions<Person>
        {
            Query = x => x.GedcomId == individual.XRefID
        });
        var person = existing.FirstOrDefault();

        var name = individual.Names.FirstOrDefault();
        string givenNames = name?.Given?.Trim() ?? "Unknown";
        string familyName = name?.Surname?.Trim() ?? string.Empty;
        bool isMale = individual.Sex == GedcomSex.Male;

        if (person is null)
        {
            person = new Person
            {
                GivenNames = givenNames,
                FamilyName = familyName,
                IsMale = isMale,
                GedcomId = individual.XRefID,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            ApplyBirthDeath(individual, person);
            ApplyFacts(individual, person);
            await personRepository.InsertAsync(person);
            summary.PeopleImported++;
        }
        else
        {
            person.GivenNames = givenNames;
            person.FamilyName = familyName;
            person.IsMale = isMale;
            ApplyBirthDeath(individual, person);
            ApplyFacts(individual, person);
            person.UpdatedAtUtc = DateTime.UtcNow;
            await personRepository.UpdateAsync(person);
            summary.PeopleUpdated++;
        }

        xrefToPersonId[individual.XRefID] = person.Id;

        await ImportPersonEventsAsync(individual, database, person.Id, summary);
    }

    private static void ApplyBirthDeath(GedcomIndividualRecord individual, Person person)
    {
        if (individual.Birth != null)
        {
            var d = ParseDate(individual.Birth.Date);
            person.YearOfBirth = d.Year;
            person.MonthOfBirth = d.Month;
            person.DayOfBirth = d.Day;
            person.PlaceOfBirth = individual.Birth.Place?.Name;
        }

        if (individual.Death != null)
        {
            var d = ParseDate(individual.Death.Date);
            person.YearOfDeath = d.Year;
            person.MonthOfDeath = d.Month;
            person.DayOfDeath = d.Day;
            person.PlaceOfDeath = individual.Death.Place?.Name;
        }
    }

    private static void ApplyFacts(GedcomIndividualRecord individual, Person person)
    {
        var occu = individual.FindEvent(GedcomEventType.OCCUFact);
        if (occu != null && !string.IsNullOrWhiteSpace(occu.Classification))
            person.Occupation = occu.Classification;

        var educ = individual.FindEvent(GedcomEventType.EDUCFact);
        if (educ != null && !string.IsNullOrWhiteSpace(educ.Classification))
            person.Education = educ.Classification;

        var reli = individual.FindEvent(GedcomEventType.RELIFact);
        if (reli != null && !string.IsNullOrWhiteSpace(reli.Classification))
            person.Religion = reli.Classification;
    }

    private async Task ImportPersonEventsAsync(
        GedcomIndividualRecord individual,
        GedcomDatabase database,
        int personId,
        GedcomImportResult summary)
    {
        // Clear old imported events before re-importing
        var existing = await eventRepository.FindAsync(new SearchOptions<PersonEvent>
        {
            Query = x => x.PersonId == personId
        });
        foreach (var e in existing)
            await eventRepository.DeleteAsync(e);

        var tagMap = new Dictionary<GedcomEventType, string>
        {
            [GedcomEventType.Birth]  = PersonEventType.Birth,
            [GedcomEventType.DEAT]   = PersonEventType.Death,
            [GedcomEventType.BURI]   = PersonEventType.Burial,
            [GedcomEventType.CREM]   = PersonEventType.Cremation,
            [GedcomEventType.CHR]    = PersonEventType.Christening,
            [GedcomEventType.BAPM]   = PersonEventType.Baptism,
            [GedcomEventType.CONF]   = PersonEventType.Confirmation,
            [GedcomEventType.ADOP]   = PersonEventType.Adoption,
            [GedcomEventType.EMIG]   = PersonEventType.Emigration,
            [GedcomEventType.IMMI]   = PersonEventType.Immigration,
            [GedcomEventType.NATU]   = PersonEventType.Naturalization,
            [GedcomEventType.DIV]    = PersonEventType.Divorce,
            [GedcomEventType.ENGA]   = PersonEventType.Engagement,
            [GedcomEventType.RESI]   = PersonEventType.Residence,
        };

        foreach (var indEvent in individual.Events)
        {
            if (!tagMap.TryGetValue(indEvent.EventType, out string? tag))
                continue;

            var d = ParseDate(indEvent.Date);
            string? noteText = ResolveFirstNote(indEvent.Notes, database);

            var evt = new PersonEvent
            {
                PersonId = personId,
                EventType = tag,
                Year = d.Year,
                Month = d.Month,
                Day = d.Day,
                Place = indEvent.Place?.Name,
                Description = indEvent.Classification,
                Note = noteText,
                CreatedAtUtc = DateTime.UtcNow
            };
            await eventRepository.InsertAsync(evt);
            summary.EventsImported++;
        }
    }

    private async Task ImportFamilyAsync(
        GedcomFamilyRecord family,
        GedcomDatabase database,
        Dictionary<string, int> xrefToPersonId,
        GedcomImportResult summary)
    {
        int? husbandId = family.Husband != null && xrefToPersonId.TryGetValue(family.Husband, out var hid) ? hid : null;
        int? wifeId    = family.Wife   != null && xrefToPersonId.TryGetValue(family.Wife,   out var wid) ? wid : null;

        if (husbandId.HasValue && wifeId.HasValue)
        {
            int low  = Math.Min(husbandId.Value, wifeId.Value);
            int high = Math.Max(husbandId.Value, wifeId.Value);

            bool alreadyLinked = await spouseRepository.ExistsAsync(x => x.PersonId == low && x.SpouseId == high);
            if (!alreadyLinked)
            {
                var spouseRecord = new PersonSpouse { PersonId = low, SpouseId = high };

                if (family.Marriage != null)
                {
                    var d = ParseDate(family.Marriage.Date);
                    spouseRecord.MarriageYear  = d.Year;
                    spouseRecord.MarriageMonth = d.Month;
                    spouseRecord.MarriageDay   = d.Day;
                }

                await spouseRepository.InsertAsync(spouseRecord);
                summary.RelationshipsImported++;

                // Add marriage as a PersonEvent on both spouses
                if (family.Marriage != null)
                {
                    var d = ParseDate(family.Marriage.Date);
                    string? place = family.Marriage.Place?.Name;
                    string? note  = ResolveFirstNote(family.Marriage.Notes, database);

                    foreach (int spousePersonId in new[] { husbandId.Value, wifeId.Value })
                    {
                        await eventRepository.InsertAsync(new PersonEvent
                        {
                            PersonId  = spousePersonId,
                            EventType = PersonEventType.Marriage,
                            Year      = d.Year,
                            Month     = d.Month,
                            Day       = d.Day,
                            Place     = place,
                            Note      = note,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        summary.EventsImported++;
                    }
                }
            }
        }

        // Link children to their parents
        foreach (string childXref in family.Children)
        {
            if (!xrefToPersonId.TryGetValue(childXref, out int childId))
                continue;

            var child = await personRepository.FindOneAsync(childId);
            if (child is null) continue;

            bool updated = false;
            if (husbandId.HasValue && child.FatherId != husbandId) { child.FatherId = husbandId; updated = true; }
            if (wifeId.HasValue   && child.MotherId != wifeId)     { child.MotherId = wifeId;    updated = true; }

            if (updated)
            {
                child.UpdatedAtUtc = DateTime.UtcNow;
                await personRepository.UpdateAsync(child);
                summary.RelationshipsImported++;
            }
        }
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public async Task<string> ExportAsync()
    {
        var people   = (await personRepository.FindAsync(new SearchOptions<Person>())).ToList();
        var spouses  = (await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>())).ToList();
        var events   = (await eventRepository.FindAsync(new SearchOptions<PersonEvent>())).ToList();

        var eventsByPerson = events.ToLookup(e => e.PersonId);
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
            sb.AppendLine($"1 SEX {(person.IsMale ? "M" : "F")}");

            if (person.YearOfBirth.HasValue)
            {
                sb.AppendLine("1 BIRT");
                sb.AppendLine($"2 DATE {FormatDate(person.YearOfBirth, person.MonthOfBirth, person.DayOfBirth)}");
                if (!string.IsNullOrWhiteSpace(person.PlaceOfBirth))
                    sb.AppendLine($"2 PLAC {person.PlaceOfBirth}");
            }

            if (person.YearOfDeath.HasValue)
            {
                sb.AppendLine("1 DEAT");
                sb.AppendLine($"2 DATE {FormatDate(person.YearOfDeath, person.MonthOfDeath, person.DayOfDeath)}");
                if (!string.IsNullOrWhiteSpace(person.PlaceOfDeath))
                    sb.AppendLine($"2 PLAC {person.PlaceOfDeath}");
            }

            if (!string.IsNullOrWhiteSpace(person.Occupation))
                sb.AppendLine($"1 OCCU {person.Occupation}");
            if (!string.IsNullOrWhiteSpace(person.Education))
                sb.AppendLine($"1 EDUC {person.Education}");
            if (!string.IsNullOrWhiteSpace(person.Religion))
                sb.AppendLine($"1 RELI {person.Religion}");
            if (!string.IsNullOrWhiteSpace(person.Bio))
                WriteNote(sb, 1, person.Bio);

            // Additional life events (skip birth/death already written above)
            foreach (var evt in eventsByPerson[person.Id]
                .Where(e => e.EventType != PersonEventType.Birth && e.EventType != PersonEventType.Death))
            {
                sb.AppendLine($"1 {evt.EventType}");
                if (evt.Year.HasValue)
                    sb.AppendLine($"2 DATE {FormatDate(evt.Year, evt.Month, evt.Day)}");
                if (!string.IsNullOrWhiteSpace(evt.Place))
                    sb.AppendLine($"2 PLAC {evt.Place}");
                if (!string.IsNullOrWhiteSpace(evt.Description))
                    sb.AppendLine($"2 TYPE {evt.Description}");
                if (!string.IsNullOrWhiteSpace(evt.Note))
                    WriteNote(sb, 2, evt.Note);
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
            var wife    = husband == p1 ? p2 : p1;

            sb.AppendLine($"0 @F{familyIndex++}@ FAM");
            if (husband != null) sb.AppendLine($"1 HUSB {husband.GedcomId ?? $"@I{husband.Id}@"}");
            if (wife    != null) sb.AppendLine($"1 WIFE {wife.GedcomId    ?? $"@I{wife.Id}@"}");

            if (spouse.MarriageYear.HasValue)
            {
                sb.AppendLine("1 MARR");
                sb.AppendLine($"2 DATE {FormatDate(spouse.MarriageYear, spouse.MarriageMonth, spouse.MarriageDay)}");
            }

            // Children whose both parent IDs match either spouse
            var children = people.Where(p =>
                (p.FatherId == spouse.PersonId || p.FatherId == spouse.SpouseId) &&
                (p.MotherId == spouse.PersonId || p.MotherId == spouse.SpouseId));

            foreach (var child in children)
                sb.AppendLine($"1 CHIL {child.GedcomId ?? $"@I{child.Id}@"}");
        }

        sb.AppendLine("0 TRLR");
        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? ResolveFirstNote(GedcomRecordList<string> noteXrefs, GedcomDatabase db)
    {
        foreach (var xref in noteXrefs)
        {
            if (db[xref] is GedcomNoteRecord note && !string.IsNullOrWhiteSpace(note.Text))
                return note.Text;
        }
        return null;
    }

    private static (short? Year, byte? Month, byte? Day) ParseDate(GedcomDate? date)
    {
        if (date?.DateTime1 == null && string.IsNullOrWhiteSpace(date?.DateString))
            return (null, null, null);

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
            return (null, null, null);

        string[] monthNames = ["JAN","FEB","MAR","APR","MAY","JUN","JUL","AUG","SEP","OCT","NOV","DEC"];

        // Strip qualifiers
        foreach (var q in new[] { "ABT", "ABOUT", "EST", "BEF", "AFT", "CAL", "CIRCA", "~", "INT", "FROM", "TO", "BET", "AND" })
        {
            if (dateString.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                dateString = dateString[q.Length..].Trim();
        }

        short? year = null; byte? month = null; byte? day = null;
        foreach (var part in dateString.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (short.TryParse(part, out short y) && y >= 1 && y <= 9999)
                year = y;
            else if (byte.TryParse(part, out byte d) && d >= 1 && d <= 31)
                day = d;
            else
            {
                int idx = Array.FindIndex(monthNames, m => m.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) month = (byte)(idx + 1);
            }
        }
        return (year, month, day);
    }

    private static string FormatDate(short? year, byte? month, byte? day)
    {
        if (year == null) return string.Empty;
        if (month == null) return year.Value.ToString("D4");
        string monthAbbr = new DateTime(year.Value, month.Value, 1).ToString("MMM").ToUpperInvariant();
        if (day == null) return $"{monthAbbr} {year:D4}";
        return $"{day:D2} {monthAbbr} {year:D4}";
    }

    private static void WriteNote(StringBuilder sb, int level, string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        sb.AppendLine($"{level} NOTE {lines[0]}");
        foreach (var line in lines.Skip(1))
            sb.AppendLine($"{level + 1} CONT {line}");
    }
}
