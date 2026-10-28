using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Microsoft.Extensions.DependencyInjection;
using Heroicsoft.NetForge.Video;

namespace Kinnect.Services;

/// <summary>
/// Reads and saves <see cref="AppSettings"/>. It is a singleton because file storage and video processing
/// need settings too and live for the whole run, so repositories are resolved from a fresh scope on each call.
/// </summary>
public sealed class SettingsService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<SettingsService> logger) : ISettingsService
{
    private readonly ConcurrentDictionary<Type, AppSettings> cache = new();
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<T> GetAsync<T>(CancellationToken cancellationToken = default) where T : AppSettings, new()
    {
        if (cache.TryGetValue(typeof(T), out var cached))
        {
            return (T)cached.Copy();
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            return cache.TryGetValue(typeof(T), out cached) ? (T)cached.Copy() : await LoadAsync<T>(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<T>> SaveAsync<T>(T settings, CancellationToken cancellationToken = default) where T : AppSettings, new()
    {
        var candidate = (T)settings.Copy();
        Normalize(candidate);

        var errors = Validate(candidate);
        if (errors.Count > 0)
        {
            return Result<T>.Invalid(errors);
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<T>>();
            var existing = await FirstAsync(repository, cancellationToken);

            T saved;
            if (existing is null)
            {
                candidate.Id = 0;
                saved = await repository.InsertAsync(candidate);
            }
            else
            {
                candidate.Id = existing.Id;
                saved = await repository.UpdateAsync(candidate);
            }

            cache[typeof(T)] = saved.Copy();
            logger.LogInformation("{Settings} saved.", typeof(T).Name);
            return Result<T>.Success((T)saved.Copy());
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<T> LoadAsync<T>(CancellationToken cancellationToken) where T : AppSettings, new()
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<T>>();

            var stored = await FirstAsync(repository, cancellationToken);
            if (stored is not null)
            {
                cache[typeof(T)] = stored.Copy();
                return (T)stored.Copy();
            }

            var imported = FromConfiguration<T>();
            if (imported is null)
            {
                var defaults = new T();
                cache[typeof(T)] = defaults.Copy();
                return defaults;
            }

            var saved = await repository.InsertAsync(imported);
            cache[typeof(T)] = saved.Copy();
            logger.LogInformation(
                "{Settings} were not in the database, so the '{Section}' configuration section was imported into it.",
                typeof(T).Name,
                saved.ConfigurationSection);
            return (T)saved.Copy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Most likely the migration that creates the table has not run yet. Don't cache, so the next call tries again.
            logger.LogWarning(ex, "Could not read {Settings} from the database. Using configuration or defaults for now.", typeof(T).Name);
            return FromConfiguration<T>() ?? new T();
        }
    }

    private static async Task<T?> FirstAsync<T>(IRepository<T> repository, CancellationToken cancellationToken) where T : AppSettings, new() =>
        await repository.FindOneAsync(new SearchOptions<T>
        {
            OrderBy = query => query.OrderBy(settings => settings.Id),
            CancellationToken = cancellationToken,
        });

    /// <summary>Null when the install never had a section for these settings. A section with only some keys keeps defaults for the rest.</summary>
    private T? FromConfiguration<T>() where T : AppSettings, new()
    {
        var value = new T();
        if (value.ConfigurationSection is not string name)
        {
            return null;
        }

        var section = configuration.GetSection(name);
        if (!section.Exists())
        {
            return null;
        }

        section.Bind(value);
        value.Id = 0;
        Normalize(value);
        return value;
    }

    private static void Normalize(AppSettings settings)
    {
        switch (settings)
        {
            case DocumentSettings documents:
                documents.AllowedExtensions = string.Join(
                    ',',
                    documents.AllowedExtensions
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(extension => extension.StartsWith('.') ? extension : "." + extension)
                        .Select(extension => extension.ToLowerInvariant())
                        .Distinct());
                break;

            case VideoSettings videos:
                videos.ToolsPath = string.IsNullOrWhiteSpace(videos.ToolsPath) ? null : videos.ToolsPath.Trim();
                videos.OutputVideoSize = videos.OutputVideoSize?.Trim() ?? string.Empty;
                break;

            case EmailSettings email:
                email.Host = email.Host?.Trim() ?? string.Empty;
                email.FromAddress = email.FromAddress?.Trim() ?? string.Empty;
                email.FromName = email.FromName?.Trim() ?? string.Empty;
                email.Username = string.IsNullOrWhiteSpace(email.Username) ? null : email.Username.Trim();
                email.Password = string.IsNullOrEmpty(email.Password) ? null : email.Password;
                break;
        }
    }

    private static List<ValidationError> Validate(AppSettings settings)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true);
        var errors = results
            .Select(result => new ValidationError(result.MemberNames.FirstOrDefault() ?? string.Empty, result.ErrorMessage ?? "Invalid value."))
            .ToList();

        switch (settings)
        {
            case DocumentSettings documents when documents.AllowedExtensionSet.Count == 0:
                errors.Add(new ValidationError(nameof(DocumentSettings.AllowedExtensions), "Allow at least one file extension."));
                break;

            case VideoSettings videos when !VideoSizes.IsKnown(videos.OutputVideoSize):
                errors.Add(new ValidationError(
                    nameof(VideoSettings.OutputVideoSize),
                    $"'{videos.OutputVideoSize}' is not a video size FFmpeg knows. Try Hd720 or Hd1080."));
                break;

            case EmailSettings email when email.FromAddress.Length > 0 && !MailAddress.TryCreate(email.FromAddress, out _):
                errors.Add(new ValidationError(nameof(EmailSettings.FromAddress), "That is not a valid email address."));
                break;
        }

        return errors;
    }
}