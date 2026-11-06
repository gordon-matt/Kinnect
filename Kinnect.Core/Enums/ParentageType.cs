namespace Kinnect.Enums;

/// <summary>
/// How a set of parents relates to the child. The number is stored, so new values go at the end.
/// GEDCOM <c>PEDI</c> is <c>birth</c>, <c>adopted</c> or <c>foster</c>. <c>sealing</c> and <c>other</c> are not supported.
/// </summary>
public enum ParentageType
{
    Birth = 0,
    Adopted = 1,
    Foster = 2,
}

/// <summary>
/// Who may see a set of parents. Kinnect only: nothing about visibility is exported or given to the family tree component.
/// The number is stored, so new values go at the end.
/// </summary>
public enum LinkVisibility
{
    /// <summary>Everyone in the family. This is the default, and what every link was before visibility existed.</summary>
    Everyone = 0,

    /// <summary>Family administrators only.</summary>
    FamilyAdmins = 1,

    /// <summary>The child only (the person whose account is linked to the child).</summary>
    ChildOnly = 2,
}

/// <summary>Which of a person's sets of parents a feature follows.</summary>
public enum ParentLineage
{
    /// <summary>The primary set the viewer can see. This is what the tree draws, and the default everywhere.</summary>
    Primary = 0,

    /// <summary>Birth sets only: "blood relatives only".</summary>
    BirthOnly = 1,

    /// <summary>Every set the viewer can see.</summary>
    Any = 2,
}

/// <summary>The place a parent holds in a set. A set has at most one of each. It also decides GEDCOM <c>HUSB</c> and <c>WIFE</c>.</summary>
public enum ParentRole
{
    Father = 0,
    Mother = 1,
}

public static class ParentageTypes
{
    public static string Label(ParentageType type) => type switch
    {
        ParentageType.Adopted => "Adopted",
        ParentageType.Foster => "Foster",
        _ => "Birth",
    };

    /// <summary>The label for a parent in a set of this type, such as "Adoptive father".</summary>
    public static string ParentLabel(ParentageType type, ParentRole role) => (type, role) switch
    {
        (ParentageType.Adopted, ParentRole.Father) => "Adoptive father",
        (ParentageType.Adopted, ParentRole.Mother) => "Adoptive mother",
        (ParentageType.Foster, ParentRole.Father) => "Foster father",
        (ParentageType.Foster, ParentRole.Mother) => "Foster mother",
        (_, ParentRole.Father) => "Father",
        _ => "Mother",
    };

    public static string Label(LinkVisibility visibility) => visibility switch
    {
        LinkVisibility.FamilyAdmins => "Family administrators only",
        LinkVisibility.ChildOnly => "The child only",
        _ => "Everyone in the family",
    };
}
