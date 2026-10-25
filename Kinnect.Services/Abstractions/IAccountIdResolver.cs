namespace Kinnect.Services.Abstractions;

/// <summary>
/// Turns a claim value into the account id stored on <c>AspNetUsers</c>.
/// A Blazor circuit and a stray HTTP request can carry different claims for the same person.
/// </summary>
public interface IAccountIdResolver
{
    /// <summary>The account id for <paramref name="claim"/>, or null when it does not belong to an account.</summary>
    string? Resolve(string claim);
}
