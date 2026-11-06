namespace Kinnect.Services.Abstractions;

/// <summary>
/// Reads and writes who parented whom, always for the signed-in viewer. A set the viewer may not see is never
/// returned, and a write that touches one is rejected here, not just hidden in the page.
/// </summary>
public interface IParentSetService
{
    /// <summary>The signed-in viewer. Anything that is not signed in is treated as <see cref="ParentViewer.Nobody"/>.</summary>
    ParentViewer Viewer { get; }

    /// <summary>
    /// Loads every set the viewer may see. With <paramref name="publicOnly"/> only the sets everyone in the family can
    /// see are returned, which is what an export includes unless it is asked for more.
    /// </summary>
    Task<ParentGraph> LoadGraphAsync(bool publicOnly = false);

    /// <summary>The sets of one child that the viewer may see, primary first.</summary>
    Task<IReadOnlyList<ParentSetDto>> GetForChildAsync(int childId);

    Task<Result<ParentSetDto>> AddAsync(int childId, ParentSetRequest request);

    Task<Result<ParentSetDto>> UpdateAsync(int setId, ParentSetRequest request);

    Task<Result> RemoveAsync(int setId);

    /// <summary>Puts a parent into a set. A set holds two at most.</summary>
    Task<Result> AddParentAsync(int setId, int parentId);

    /// <summary>Takes a parent out of a set. A set left with nobody in it is removed.</summary>
    Task<Result> RemoveParentAsync(int setId, int parentId);

    /// <summary>
    /// Sets the birth parents of a child: the one thing the older father and mother fields could say. The birth set
    /// the viewer sees is changed, or one is added. Both null removes it.
    /// </summary>
    Task<Result> SetBirthParentsAsync(int childId, int? fatherId, int? motherId);

    /// <summary>
    /// Makes the sets the viewer can see for one child match <paramref name="desired"/>, which is what the family tree
    /// component hands back. Sets the viewer cannot see are left alone.
    /// </summary>
    Task<Result> SyncAsync(int childId, IReadOnlyList<ParentSetSync> desired);

    /// <summary>
    /// Takes a person out of every set they are a parent in, and removes the sets that leaves empty.
    /// Call it before deleting the person.
    /// </summary>
    Task DetachParentAsync(int personId);
}

/// <summary>A set of parents as the family tree component describes it.</summary>
/// <param name="Id">The stored id as text, or anything else (such as the component's own id) for a set that is new.</param>
/// <param name="Type">Birth, adopted or foster.</param>
/// <param name="ParentIds">One or two parents.</param>
/// <param name="Year">The adoption or placement year, if known.</param>
/// <param name="Month">The month, if known.</param>
/// <param name="Day">The day, if known.</param>
/// <param name="IsPrimary">True when the set is the one drawn by default.</param>
public sealed record ParentSetSync(
    string? Id,
    ParentageType Type,
    IReadOnlyList<int> ParentIds,
    short? Year,
    byte? Month,
    byte? Day,
    bool IsPrimary);
