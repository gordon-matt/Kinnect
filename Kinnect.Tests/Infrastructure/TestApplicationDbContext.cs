namespace Kinnect.Data;

/// <summary>
/// Test stand-in for the provider contexts. Tests open a SQLite database.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContextBase(options);
