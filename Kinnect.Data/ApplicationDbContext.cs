using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Kinnect.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    public DbSet<Person> People { get; set; }

    public DbSet<PersonSpouse> PersonSpouses { get; set; }

    public DbSet<PersonVersion> PersonVersions { get; set; }

    public DbSet<Post> Posts { get; set; }

    public DbSet<Photo> Photos { get; set; }

    public DbSet<PersonPhoto> PersonPhotos { get; set; }

    public DbSet<PhotoTag> PhotoTags { get; set; }

    public DbSet<Video> Videos { get; set; }

    public DbSet<VideoTag> VideoTags { get; set; }

    public DbSet<Document> Documents { get; set; }

    public DbSet<DocumentTag> DocumentTags { get; set; }

    public DbSet<Tag> Tags { get; set; }

    public DbSet<PersonEvent> PersonEvents { get; set; }

    public DbSet<PhotoEvent> PhotoEvents { get; set; }

    public DbSet<VideoEvent> VideoEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}