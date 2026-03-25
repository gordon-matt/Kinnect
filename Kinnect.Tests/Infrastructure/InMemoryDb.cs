namespace Kinnect.Tests.Infrastructure;

public static class InMemoryDb
{
    public static (DbContextOptions<ApplicationDbContext> Options, TestDbContextFactory Factory) Create()
    {
        string name = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
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