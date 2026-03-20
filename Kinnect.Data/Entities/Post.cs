using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kinnect.Data.Entities;

public class Post : BaseEntity<int>
{
    public int AuthorPersonId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual Person Author { get; set; } = null!;
}

public class PostMap : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Content).IsRequired();

        builder.HasOne(m => m.Author)
            .WithMany(m => m.Posts)
            .HasForeignKey(m => m.AuthorPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.CreatedAtUtc);
    }
}
