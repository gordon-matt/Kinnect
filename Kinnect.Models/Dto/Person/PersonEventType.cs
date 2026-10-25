namespace Kinnect.Models.Dto;

public static class PersonEventType
{
    public const string Birth = "BIRT";
    public const string Death = "DEAT";
    public const string Burial = "BURI";
    public const string Cremation = "CREM";
    public const string Christening = "CHR";
    public const string Baptism = "BAPM";
    public const string BarMitzvah = "BARM";
    public const string BasMitzvah = "BASM";
    public const string Blessing = "BLES";
    public const string AdultChristening = "CHRA";
    public const string Confirmation = "CONF";
    public const string FirstCommunion = "FCOM";
    public const string Ordination = "ORDN";
    public const string Adoption = "ADOP";
    public const string Marriage = "MARR";
    public const string Divorce = "DIV";
    public const string DivorceFiled = "DIVF";
    public const string Annulment = "ANUL";
    public const string Engagement = "ENGA";
    public const string MarriageBanns = "MARB";
    public const string MarriageContract = "MARC";
    public const string MarriageLicense = "MARL";
    public const string MarriageSettlement = "MARS";
    public const string Naturalization = "NATU";
    public const string Emigration = "EMIG";
    public const string Immigration = "IMMI";
    public const string Census = "CENS";
    public const string Probate = "PROB";
    public const string Will = "WILL";
    public const string Graduation = "GRAD";
    public const string Retirement = "RETI";
    public const string Occupation = "OCCU";
    public const string Education = "EDUC";
    public const string Religion = "RELI";
    public const string Residence = "RESI";
    public const string Caste = "CAST";
    public const string PhysicalDescription = "DSCR";
    public const string IdentificationNumber = "IDNO";
    public const string Nationality = "NATI";
    public const string NumberOfChildren = "NCHI";
    public const string NumberOfMarriages = "NMR";
    public const string Property = "PROP";
    public const string SocialSecurityNumber = "SSN";
    public const string Title = "TITL";
    public const string Fact = "FACT";

    /// <summary>GEDCOM <c>EVEN</c>, a user-defined event. The description says what kind it is.</summary>
    public const string Custom = "EVEN";

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [Birth] = "Birth",
        [Death] = "Death",
        [Burial] = "Burial",
        [Cremation] = "Cremation",
        [Christening] = "Christening",
        [Baptism] = "Baptism",
        [BarMitzvah] = "Bar Mitzvah",
        [BasMitzvah] = "Bas Mitzvah",
        [Blessing] = "Blessing",
        [AdultChristening] = "Adult Christening",
        [Confirmation] = "Confirmation",
        [FirstCommunion] = "First Communion",
        [Ordination] = "Ordination",
        [Adoption] = "Adoption",
        [Marriage] = "Marriage",
        [Divorce] = "Divorce",
        [DivorceFiled] = "Divorce Filed",
        [Annulment] = "Annulment",
        [Engagement] = "Engagement",
        [MarriageBanns] = "Marriage Banns",
        [MarriageContract] = "Marriage Contract",
        [MarriageLicense] = "Marriage License",
        [MarriageSettlement] = "Marriage Settlement",
        [Naturalization] = "Naturalization",
        [Emigration] = "Emigration",
        [Immigration] = "Immigration",
        [Census] = "Census",
        [Probate] = "Probate",
        [Will] = "Will",
        [Graduation] = "Graduation",
        [Retirement] = "Retirement",
        [Occupation] = "Occupation",
        [Education] = "Education",
        [Religion] = "Religion",
        [Residence] = "Residence",
        [Caste] = "Caste",
        [PhysicalDescription] = "Physical Description",
        [IdentificationNumber] = "Identification Number",
        [Nationality] = "Nationality",
        [NumberOfChildren] = "Number of Children",
        [NumberOfMarriages] = "Number of Marriages",
        [Property] = "Property",
        [SocialSecurityNumber] = "Social Security Number",
        [Title] = "Title",
        [Fact] = "Fact",
        [Custom] = "Custom Event",
    };

    public static string GetLabel(string type) =>
        Labels.TryGetValue(type, out string? label) ? label : type;

    /// <summary>Types that belong on the person timeline. Marriage, divorce and engagement live on the spouse relationship.</summary>
    public static IEnumerable<(string Type, string Label)> TimelineSelectableTypes =>
        Labels.Where(kv => !NonTimelineEventTypes.Contains(kv.Key))
            .Select(kv => (kv.Key, kv.Value));

    private static readonly HashSet<string> NonTimelineEventTypes =
    [
        Marriage, Divorce, Engagement,
    ];

    private static readonly HashSet<string> SingleInstanceTimelineEventTypes =
    [
        Birth, Death, Christening, Burial, Cremation,
    ];

    private static readonly HashSet<string> CopyableToRelatives =
    [
        Residence, Emigration, Immigration, Naturalization, Census, Custom,
    ];

    /// <summary>Stored on the spouse relationship, not as a person event.</summary>
    public static bool IsNonTimelineEventType(string eventType) =>
        NonTimelineEventTypes.Contains(eventType.ToUpperInvariant());

    /// <summary>Timeline event types that can only exist once per person.</summary>
    public static bool IsSingleInstanceTimelineEventType(string eventType) =>
        SingleInstanceTimelineEventTypes.Contains(eventType.ToUpperInvariant());

    /// <summary>Events that can sensibly be copied onto relatives, such as a shared residence.</summary>
    public static bool CanCopyToRelatives(string eventType) =>
        CopyableToRelatives.Contains(eventType.ToUpperInvariant());
}
