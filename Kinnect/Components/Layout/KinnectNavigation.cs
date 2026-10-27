using EasyChatz.Components;
using Heroicsoft.NetForge.Abstractions.Navigation;

namespace Kinnect.Components.Layout;

public sealed class KinnectNavigation(IUserContextService userContext) : IShellNavigation
{
    public string? SearchUrlFormat => "/people?q={0}";

    public Task<IReadOnlyList<ForgeNavSection>> GetSectionsAsync(CancellationToken cancellationToken = default)
    {
        bool admin = userContext.IsAdmin();
        var sections = new List<ForgeNavSection>
        {
            new()
            {
                Items =
                [
                    new ForgeNavItem { Title = "Home", Href = "/", Icon = "bi-house", MatchAll = true },
                    new ForgeNavItem { Title = "Family tree", Href = "/tree", Icon = "bi-diagram-3" },
                    new ForgeNavItem { Title = "Map", Href = "/map", Icon = "bi-geo-alt" },
                    new ForgeNavItem
                    {
                        Title = "Messages",
                        Href = "/messages",
                        Icon = "bi-chat",
                        BadgeComponent = typeof(ChatUnreadBadge),
                        BadgeParameters = new Dictionary<string, object>
                        {
                            ["Theme"] = "netforge",
                            ["Class"] = "nf-nav-badge",
                            // Keeps the user shown as online while they are on other pages.
                            ["TrackPresence"] = true,
                        },
                    },
                    new ForgeNavItem { Title = "My profile", Href = "/me", Icon = "bi-person" },
                ],
            },
        };

        if (admin)
        {
            var adminItems = new List<ForgeNavItem>
            {
                new() { Title = "Person backups", Href = "/admin/backups", Icon = "bi-archive" },
                new() { Title = "Settings", Href = "/admin/settings", Icon = "bi-gear" },
                new() { Title = "Logs", Href = "/logvue", Icon = "bi-journal-text", External = true },
                new() { Title = "Jobs", Href = "/hangfire", Icon = "bi-clock-history", External = true },
            };
            if (!Constants.UseKeyCloak)
            {
                adminItems.Insert(0, new ForgeNavItem { Title = "Users", Href = "/admin/users", Icon = "bi-people" });
            }

            sections.Add(new ForgeNavSection { Heading = "Admin", Roles = Constants.Roles.Administrator, Items = adminItems });
        }

        return Task.FromResult<IReadOnlyList<ForgeNavSection>>(sections);
    }
}