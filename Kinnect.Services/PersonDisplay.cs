using MyWebTree.Models;

namespace Kinnect.Services;

/// <summary>
/// Names as Kinnect shows them. <see cref="Tree"/> is set at startup from the settings cache so a
/// synchronous mapper can follow the admin's choice. Tests leave it unset and get the defaults,
/// which capitalize family names and put given names first.
/// </summary>
public static class PersonDisplay
{
    public static Func<TreeSettings>? Tree { get; set; }

    public static TreeSettings TreeSettings => Tree?.Invoke() ?? new TreeSettings();

    public static string Name(
        string? givenNames,
        string? familyName,
        bool isFamilyNameFirst,
        bool editing = false,
        string? prefix = null,
        string? suffix = null) =>
        PersonName.Format(
            givenNames,
            familyName,
            isFamilyNameFirst,
            capitalizeFamilyName: !editing && TreeSettings.CapitalizeFamilyNames,
            prefix,
            suffix);

    public static string Name(Person? person, bool editing = false) =>
        person is null
            ? string.Empty
            : Name(person.GivenNames, person.FamilyName, person.IsFamilyNameFirst, editing, person.NamePrefix, person.NameSuffix);

    public static string Name(PersonDto person, bool editing = false) =>
        Name(person.GivenNames, person.FamilyName, person.IsFamilyNameFirst, editing, person.NamePrefix, person.NameSuffix);
}
