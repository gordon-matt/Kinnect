namespace Kinnect.Services.Abstractions;

/// <summary>
/// Application settings kept in the database: <see cref="ImageSettings"/>, <see cref="DocumentSettings"/>,
/// <see cref="VideoSettings"/>, <see cref="EmailSettings"/> and <see cref="TreeSettings"/>.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// The saved settings. The database comes first. When it has none, the older appsettings / docker compose
    /// values are imported into it, and when there are none of those either, the defaults are returned.
    /// The result is a copy, so changing it does not change what other callers see until it is saved.
    /// </summary>
    Task<T> GetAsync<T>(CancellationToken cancellationToken = default) where T : AppSettings, new();

    /// <summary>Checks and saves the settings. Everyone sees the new values straight away.</summary>
    Task<Result<T>> SaveAsync<T>(T settings, CancellationToken cancellationToken = default) where T : AppSettings, new();
}
