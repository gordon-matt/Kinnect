using Microsoft.Extensions.Configuration;

namespace Kinnect.Data;

public class ApplicationDbContextFactory(IConfiguration configuration) : IDbContextFactory
{
    private DbContextOptions<ApplicationDbContext>? _options;

    private DbContextOptions<ApplicationDbContext> Options
    {
        get
        {
            if (_options is null)
            {
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                string? connectionString = configuration.GetConnectionString("DefaultConnection");
                optionsBuilder.UseNpgsql(connectionString);
                _options = optionsBuilder.Options;
            }
            return _options;
        }
    }

    public DbContext GetContext() => new ApplicationDbContext(Options);

    public DbContext GetContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
