namespace Kinnect.Enums;

/// <summary>
/// A way to reach a person. The number is stored, so new values go at the end.
/// Phone and Mobile both export as GEDCOM <c>PHON</c>.
/// </summary>
public enum ContactType
{
    Email = 0,
    Phone = 1,
    Mobile = 2,
    Fax = 3,
    Website = 4,
    Address = 5,
}

public static class ContactTypes
{
    public static string Label(ContactType type) => type switch
    {
        ContactType.Email => "Email",
        ContactType.Phone => "Phone",
        ContactType.Mobile => "Mobile",
        ContactType.Fax => "Fax",
        ContactType.Website => "Website",
        ContactType.Address => "Address",
        _ => type.ToString(),
    };

    /// <summary>The GEDCOM tag written for this contact.</summary>
    public static string GedcomTag(ContactType type) => type switch
    {
        ContactType.Email => "EMAIL",
        ContactType.Phone or ContactType.Mobile => "PHON",
        ContactType.Fax => "FAX",
        ContactType.Website => "WWW",
        ContactType.Address => "ADDR",
        _ => "WWW",
    };

    /// <summary>Maps a label from an older contact row or backup. Anything unrecognised is a website.</summary>
    public static ContactType FromLabel(string? name)
    {
        string key = name?.Trim().ToLowerInvariant() ?? string.Empty;
        return key switch
        {
            "email" or "e-mail" => ContactType.Email,
            "phone" or "telephone" or "tel" => ContactType.Phone,
            "mobile" or "cell" or "cellphone" or "cell phone" => ContactType.Mobile,
            "fax" => ContactType.Fax,
            "address" or "postal" => ContactType.Address,
            _ => ContactType.Website,
        };
    }
}
