using Kinnect.Enums;

namespace Kinnect.Models.Requests;

public sealed class ContactInput
{
    public ContactType Type { get; set; }

    public string Value { get; set; } = string.Empty;
}