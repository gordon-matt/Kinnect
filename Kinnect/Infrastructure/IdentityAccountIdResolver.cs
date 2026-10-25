using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kinnect.Infrastructure;

/// <summary>
/// The signed-in claim is sometimes the account id and sometimes the email or username.
/// Linking and "my profile" both have to use the id stored on the user row.
/// </summary>
public sealed class IdentityAccountIdResolver(IServiceScopeFactory scopes) : IAccountIdResolver
{
    public string? Resolve(string claim)
    {
        if (string.IsNullOrWhiteSpace(claim))
        {
            return null;
        }

        // Off the Blazor renderer so the user-store query cannot deadlock that context.
        return Task.Run(() => LookupAsync(claim.Trim())).GetAwaiter().GetResult();
    }

    private async Task<string?> LookupAsync(string claim)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var byId = await users.FindByIdAsync(claim);
        if (byId is not null)
        {
            return byId.Id;
        }

        var byEmail = await users.FindByEmailAsync(claim);
        if (byEmail is not null)
        {
            return byEmail.Id;
        }

        var byName = await users.FindByNameAsync(claim);
        return byName?.Id;
    }
}
