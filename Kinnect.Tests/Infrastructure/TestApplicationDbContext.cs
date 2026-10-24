namespace Kinnect.Data;

/// <summary>
/// In-memory stand-in for the provider contexts. Tests never open a real database.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContextBase(options);
