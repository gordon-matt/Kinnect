namespace Kinnect.Data.Entities;

public class VideoTag : IEntity
{
    public required int VideoId { get; set; }

    public required int TagId { get; set; }

    public virtual Video Video { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;

    [IgnoreDataMember]
    public object[] KeyValues => [VideoId, TagId];
}

public class VideoTagMap : IEntityTypeConfiguration<VideoTag>
{
    public void Configure(EntityTypeBuilder<VideoTag> builder)
    {
        builder.ToTable("VideoTags", "app");
        builder.HasKey(m => new { m.VideoId, m.TagId });

        builder.HasOne(m => m.Video)
            .WithMany(m => m.VideoTags)
            .HasForeignKey(m => m.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Tag)
            .WithMany(m => m.VideoTags)
            .HasForeignKey(m => m.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}