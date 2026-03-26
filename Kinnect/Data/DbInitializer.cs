using Microsoft.AspNetCore.Identity;

namespace Kinnect.Data;

public static class DbInitializer
{
    public const string SeedAdminEmailKey = "SeedAdmin:Email";
    public const string SeedAdminPasswordKey = "SeedAdmin:Password";

    public static async Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration)
    {
        bool isFirstRun = await SeedRolesAsync(roleManager);
        if (isFirstRun)
        {
            await SeedAdminUserAsync(context, userManager, configuration);
        }
    }

    private static async Task<bool> SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        string[] roleNames = [Constants.Roles.Administrator, Constants.Roles.User];

        bool isFirstRun = false;

        foreach (string roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                isFirstRun = true;
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            }
        }

        return isFirstRun;
    }

    private static async Task SeedAdminUserAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        string adminEmail = string.IsNullOrEmpty(configuration[SeedAdminEmailKey])
            ? "admin@kinnect.local"
            : configuration[SeedAdminEmailKey]!;

        string adminPassword = string.IsNullOrEmpty(configuration[SeedAdminPasswordKey])
            ? "Admin@123"
            : configuration[SeedAdminPasswordKey]!;

        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Constants.Roles.Administrator);
                await SeedAnnouncementsRoomAsync(context, adminUser.Id);
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(adminUser, Constants.Roles.Administrator))
            {
                await userManager.AddToRoleAsync(adminUser, Constants.Roles.Administrator);
            }
        }
    }

    public static async Task SeedAnnouncementsRoomAsync(ApplicationDbContext context, string adminUserId)
    {
        bool announcementsExists = await context.ChatRooms
            .AnyAsync(r => r.Name == Constants.Chat.AnnouncementsRoomName);

        if (announcementsExists)
        {
            return;
        }

        context.ChatRooms.Add(new ChatRoom
        {
            Name = Constants.Chat.AnnouncementsRoomName,
            AdminUserId = adminUserId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

}