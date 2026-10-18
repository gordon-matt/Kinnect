namespace Kinnect.Data.Entities;

public class VideoEvent : IEntity
{
    public int VideoId { get; set; }

    public int PersonEventId { get; set; }

    public virtual Video Video { get; set; } = null!;

    public virtual PersonEvent PersonEvent { get; set; } = null!;

    [IgnoreDataMember]
    public object[] KeyValues => [VideoId, PersonEventId];
}

public class VideoEventMap : IEntityTypeConfiguration<VideoEvent>
{
    public void Configure(EntityTypeBuilder<VideoEvent> builder)
    {
        builder.ToTable("VideoEvents", "app");
        builder.HasKey(m => new { m.VideoId, m.PersonEventId });

        builder.HasOne(m => m.Video)
            .WithMany()
            .HasForeignKey(m => m.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.PersonEvent)
            .WithMany()
            .HasForeignKey(m => m.PersonEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}