namespace Kinnect.Data.Entities;

/// <summary>
/// Base for the settings tables (<see cref="ImageSettings"/>, <see cref="DocumentSettings"/>,
/// <see cref="VideoSettings"/>, <see cref="EmailSettings"/>, <see cref="TreeSettings"/>,
/// <see cref="BackupSettings"/>). Each table holds one row.
/// The settings are flat, so a shallow copy is a full copy.
/// </summary>
public abstract class AppSettings : BaseEntity<int>
{
    /// <summary>
    /// The configuration section older installs keep these values in (appsettings.json or docker compose
    /// environment variables). Null when there never was one.
    /// </summary>
    public abstract string? ConfigurationSection { get; }

    public AppSettings Copy() => (AppSettings)MemberwiseClone();
}
