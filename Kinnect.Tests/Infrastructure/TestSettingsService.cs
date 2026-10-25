using Kinnect.Services.Abstractions;

namespace Kinnect.Tests.Infrastructure;

/// <summary>Settings held in memory, for tests of services that only read them.</summary>
public sealed class TestSettingsService(params AppSettings[] initial) : ISettingsService
{
    private readonly Dictionary<Type, AppSettings> store = initial.ToDictionary(settings => settings.GetType());

    public Task<T> GetAsync<T>(CancellationToken cancellationToken = default) where T : AppSettings, new() =>
        Task.FromResult(store.TryGetValue(typeof(T), out var found) ? (T)found.Copy() : new T());

    public Task<Result<T>> SaveAsync<T>(T settings, CancellationToken cancellationToken = default) where T : AppSettings, new()
    {
        store[typeof(T)] = settings.Copy();
        return Task.FromResult(Result<T>.Success(settings));
    }
}
