using Microsoft.AspNetCore.Identity;

namespace Kinnect.Data;

public static class DbInitializer
{
    public const string SeedAdminEmailKey = "SeedAdmin:Email";
    public const string SeedAdminPasswordKey = "SeedAdmin:Password";
    public const string SeedAdminFirstNameKey = "SeedAdmin:FirstName";
    public const string SeedAdminLastNameKey = "SeedAdmin:LastName";

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

        await SeedAnnouncementsRoomAsync(context, userManager, configuration);
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

        string firstName = string.IsNullOrEmpty(configuration[SeedAdminFirstNameKey])
            ? "Admin"
            : configuration[SeedAdminFirstNameKey]!;

        string lastName = string.IsNullOrEmpty(configuration[SeedAdminLastNameKey])
            ? "User"
            : configuration[SeedAdminLastNameKey]!;

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

                var person = new Person
                {
                    UserId = adminUser.Id,
                    GivenNames = firstName,
                    FamilyName = lastName,
                    IsMale = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                context.People.Add(person);
                await context.SaveChangesAsync();
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

    private static async Task SeedAnnouncementsRoomAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        bool announcementsExists = await context.ChatRooms
            .AnyAsync(r => r.Name == Constants.Chat.AnnouncementsRoomName);

        if (announcementsExists)
        {
            return;
        }

        string adminEmail = string.IsNullOrEmpty(configuration[SeedAdminEmailKey])
            ? "admin@kinnect.local"
            : configuration[SeedAdminEmailKey]!;

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        adminUser ??= (await userManager.GetUsersInRoleAsync(Constants.Roles.Administrator)).FirstOrDefault();

        if (adminUser is null)
        {
            return;
        }

        context.ChatRooms.Add(new ChatRoom
        {
            Name = Constants.Chat.AnnouncementsRoomName,
            AdminUserId = adminUser.Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }
}