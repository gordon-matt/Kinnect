using Kinnect.Enums;

namespace Kinnect.Models.Requests;

/// <summary>
/// A whole set of parents. Adding uses every field. Updating replaces the type, the parents, the date and the
/// visibility with what is here, so read the set first when only one of them should change.
/// </summary>
public class ParentSetRequest
{
    public ParentageType Type { get; set; }

    /// <summary>At least one of <see cref="FatherId"/> and <see cref="MotherId"/> is needed.</summary>
    public int? FatherId { get; set; }

    public int? MotherId { get; set; }

    /// <summary>When the adoption or placement happened. Birth sets leave this empty.</summary>
    public short? Year { get; set; }

    public byte? Month { get; set; }

    public byte? Day { get; set; }

    /// <summary>
    /// Only administrators and the child can choose anything but <see cref="LinkVisibility.Everyone"/>.
    /// For anybody else the visibility stays as it is.
    /// </summary>
    public LinkVisibility Visibility { get; set; }

    /// <summary>Makes this the primary set, the one the tree draws first. The first set a child gets is always primary.</summary>
    public bool MakePrimary { get; set; }
}
