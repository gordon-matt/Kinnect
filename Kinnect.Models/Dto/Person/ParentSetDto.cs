using Kinnect.Enums;

namespace Kinnect.Models.Dto;

/// <summary>A set of parents of one child, as the signed-in viewer is allowed to see it.</summary>
public class ParentSetDto
{
    public int Id { get; set; }

    public int ChildPersonId { get; set; }

    public ParentageType Type { get; set; }

    public bool IsPrimary { get; set; }

    /// <summary>When the adoption or placement happened.</summary>
    public short? Year { get; set; }

    public byte? Month { get; set; }

    public byte? Day { get; set; }

    public LinkVisibility Visibility { get; set; }

    /// <summary>The parents, father first.</summary>
    public List<ParentSetMemberDto> Members { get; set; } = [];

    public int? FatherId => Members.FirstOrDefault(member => member.Role == ParentRole.Father)?.PersonId;

    public int? MotherId => Members.FirstOrDefault(member => member.Role == ParentRole.Mother)?.PersonId;
}

public class ParentSetMemberDto
{
    public int PersonId { get; set; }

    public ParentRole Role { get; set; }
}
