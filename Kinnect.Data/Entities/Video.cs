namespace Kinnect.Data.Entities;

public class Video : BaseEntity<int>
{
    public string Title { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string? ThumbnailPath { get; set; }

    public string? Description { get; set; }

    public TimeSpan? Duration { get; set; }

    public int UploadedByPersonId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int? FolderId { get; set; }

    /// <summary>True while a background job is compressing or re-encoding the uploaded file.</summary>
    public bool IsProcessing { get; set; }

    public virtual Person UploadedBy { get; set; } = null!;

    public virtual MediaFolder? Folder { get; set; }

    public virtual ICollection<VideoTag> VideoTags { get; set; } = [];
}

public class VideoMap : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.ToTable("Videos", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Title).IsRequired().HasMaxLength(500);
        builder.Property(m => m.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(m => m.ThumbnailPath).HasMaxLength(1000);

        builder.HasOne(m => m.UploadedBy)
            .WithMany()
            .HasForeignKey(m => m.UploadedByPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Folder)
            .WithMany(m => m.Videos)
            .HasForeignKey(m => m.FolderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(m => m.IsProcessing).IsRequired();
    }
}