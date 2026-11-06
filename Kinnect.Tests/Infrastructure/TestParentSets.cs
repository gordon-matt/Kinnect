using Kinnect.Services.Abstractions;

namespace Kinnect.Tests.Infrastructure;

/// <summary>A signed-in (or anonymous) user for tests, in place of the HTTP context.</summary>
public sealed class TestUserContext(string? userId = null, bool isAdmin = false, bool isEditor = false) : IUserContextService
{
    /// <summary>An administrator, who sees every set of parents except ones only the child may see.</summary>
    public static TestUserContext Admin { get; } = new("admin", isAdmin: true, isEditor: true);

    public string? GetCurrentUserId() => userId;

    public bool IsAdmin() => isAdmin;

    public bool IsEditor() => isEditor || isAdmin;

    public bool IsAuthenticated() => userId is not null;
}

/// <summary>Builds the parent set service over a test database and writes sets of parents straight into it.</summary>
public static class TestParentSets
{
    public static ParentSetService Create(TestDbContextFactory factory, IUserContextService? user = null) => new(
        new EntityFrameworkRepository<ParentSet>(factory),
        new EntityFrameworkRepository<ParentSetMember>(factory),
        new EntityFrameworkRepository<Person>(factory),
        new EntityFrameworkRepository<PersonEvent>(factory),
        user ?? TestUserContext.Admin);

    /// <summary>
    /// Records a set of parents for a child, skipping the service's checks, so a test can set up any shape
    /// of data. A father is stored in the Father role and a mother in the Mother role.
    /// </summary>
    public static async Task<ParentSet> LinkAsync(
        ApplicationDbContext db,
        Person child,
        Person? father,
        Person? mother = null,
        ParentageType type = ParentageType.Birth,
        bool isPrimary = true,
        LinkVisibility visibility = LinkVisibility.Everyone)
    {
        var set = new ParentSet
        {
            ChildPersonId = child.Id,
            Type = type,
            IsPrimary = isPrimary,
            Visibility = visibility,
            CreatedAtUtc = DateTime.UtcNow,
        };
        if (father is not null)
        {
            set.Members.Add(new ParentSetMember { ParentPersonId = father.Id, Role = ParentRole.Father });
        }

        if (mother is not null)
        {
            set.Members.Add(new ParentSetMember { ParentPersonId = mother.Id, Role = ParentRole.Mother });
        }

        db.ParentSets.Add(set);
        await db.SaveChangesAsync();
        return set;
    }
}