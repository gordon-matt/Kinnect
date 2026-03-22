namespace Kinnect.Models;

public static class PersonEventType
{
    public const string Birth = "BIRT";
    public const string Death = "DEAT";
    public const string Burial = "BURI";
    public const string Cremation = "CREM";
    public const string Christening = "CHR";
    public const string Baptism = "BAPM";
    public const string Confirmation = "CONF";
    public const string Adoption = "ADOP";
    public const string Marriage = "MARR";
    public const string Divorce = "DIV";
    public const string Engagement = "ENGA";
    public const string MarriageBanns = "MARB";
    public const string MarriageLicense = "MARL";
    public const string Naturalization = "NATU";
    public const string Emigration = "EMIG";
    public const string Immigration = "IMMI";
    public const string Occupation = "OCCU";
    public const string Education = "EDUC";
    public const string Religion = "RELI";
    public const string Residence = "RESI";
    public const string Custom = "EVEN";

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [Birth] = "Birth",
        [Death] = "Death",
        [Burial] = "Burial",
        [Cremation] = "Cremation",
        [Christening] = "Christening",
        [Baptism] = "Baptism",
        [Confirmation] = "Confirmation",
        [Adoption] = "Adoption",
        [Marriage] = "Marriage",
        [Divorce] = "Divorce",
        [Engagement] = "Engagement",
        [MarriageBanns] = "Marriage Banns",
        [MarriageLicense] = "Marriage License",
        [Naturalization] = "Naturalization",
        [Emigration] = "Emigration",
        [Immigration] = "Immigration",
        [Occupation] = "Occupation",
        [Education] = "Education",
        [Religion] = "Religion",
        [Residence] = "Residence",
        [Custom] = "Custom Event",
    };

    public static string GetLabel(string type) =>
        Labels.TryGetValue(type, out var label) ? label : type;

    /// <summary>Types that belong on the person timeline (excludes occupation/education/religion and marriage/divorce, which use other data).</summary>
    public static IEnumerable<(string Type, string Label)> TimelineSelectableTypes =>
        Labels.Where(kv => !NonTimelineEventTypes.Contains(kv.Key))
            .Select(kv => (kv.Key, kv.Value));

    private static readonly HashSet<string> NonTimelineEventTypes =
    [
        Occupation, Education, Religion, Marriage, Divorce, Engagement,
    ];

    /// <summary>Stored on <see cref="PersonSpouse"/> or profile fields, not as <see cref="PersonEventDto"/>.</summary>
    public static bool IsNonTimelineEventType(string eventType) =>
        NonTimelineEventTypes.Contains(eventType.ToUpperInvariant());
}

public class PersonEventDto
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public string EventType { get; set; } = null!;

    public string EventTypeLabel => PersonEventType.GetLabel(EventType);

    public short? Year { get; set; }

    public byte? Month { get; set; }

    public byte? Day { get; set; }

    public string? Place { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Description { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? DateDisplay
    {
        get
        {
            if (Year == null) return null;
            if (Month != null && Day != null)
                return $"{Year:D4}-{Month:D2}-{Day:D2}";
            return Year.Value.ToString("D4");
        }
    }
}

public class PersonEventRequest
{
    public string EventType { get; set; } = null!;

    public short? Year { get; set; }

    public byte? Month { get; set; }

    public byte? Day { get; set; }

    public string? Place { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Description { get; set; }

    public string? Note { get; set; }
}
