using Kinnect.Enums;

namespace Kinnect.Models.Dto;

public sealed class ContactDto
{
    public int Id { get; set; }

    public ContactType Type { get; set; }

    public string Value { get; set; } = string.Empty;
}
