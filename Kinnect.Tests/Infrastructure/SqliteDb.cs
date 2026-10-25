using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kinnect.Tests.Infrastructure;

/// <summary>
/// One SQLite database per test. A shared in-memory cache lets every context see the same
/// rows, and the keep-alive connection stays open for as long as the options are in use.
/// </summary>
public static class SqliteDb
{
    private static readonly ConditionalWeakTable<DbContextOptions, SqliteConnection> KeepAlive = new();

    public static (DbContextOptions<ApplicationDbContext> Options, TestDbContextFactory Factory) Create()
    {
        string name = Guid.NewGuid().ToString("N");
        string connectionString = $"Data Source={name};Mode=Memory;Cache=Shared;Pooling=False";

        var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .ConfigureWarnings(w => w.Ignore(SqliteEventId.SchemaConfiguredWarning))
            .Options;

        KeepAlive.Add(options, keepAlive);

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
        }

        return (options, new TestDbContextFactory(options));
    }

    public static ApplicationDbContext CreateContext(DbContextOptions<ApplicationDbContext> options) =>
        new(options);

    public static ApplicationUser CreateUser(string id, string userName = "user", string email = "u@test.com") =>
        new()
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
}
