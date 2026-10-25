namespace Kinnect.Tests.Infrastructure;

/// <summary>
/// Extenso <see cref="IDbContextFactory"/> backed by SQLite for tests.
/// </summary>
public sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory
{
    public DbContext GetContext() => new ApplicationDbContext(options);

    public DbContext GetContext(string connectionString) => GetContext();
}