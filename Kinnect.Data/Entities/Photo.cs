namespace Kinnect.Data.Entities;

public class Photo : BaseEntity<int>
{
    public string Title { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string? ThumbnailPath { get; set; }

    public string? Description { get; set; }

    public int UploadedByPersonId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Optional date the photo was taken (partial dates allowed).</summary>
    public short? YearTaken { get; set; }

    public byte? MonthTaken { get; set; }

    public byte? DayTaken { get; set; }

    /// <summary>Stores Annotorious W3C Web Annotation JSON for image annotations.</summary>
    public string? AnnotationsJson { get; set; }

    /// <summary>GPS latitude extracted from EXIF data at upload time, if available.</summary>
    public double? Latitude { get; set; }

    /// <summary>GPS longitude extracted from EXIF data at upload time, if available.</summary>
    public double? Longitude { get; set; }

    public bool LatLongAcquiredFromExif { get; set; }

    public int? FolderId { get; set; }

    /// <summary>
    /// The <c>FILE</c> value this photo had in the imported GEDCOM (e.g. <c>photos/Louis_XIV.jpg</c>), so a later
    /// export can find the original <c>OBJE</c> record. Null for photos uploaded in Kinnect.
    /// </summary>
    public string? GedcomFileReference { get; set; }

    public virtual Person UploadedBy { get; set; } = null!;

    public virtual MediaFolder? Folder { get; set; }

    public virtual ICollection<PersonPhoto> PersonPhotos { get; set; } = [];

    public virtual ICollection<PhotoTag> PhotoTags { get; set; } = [];
}

public class PhotoMap : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("Photos", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Title).IsRequired().HasMaxLength(500);
        builder.Property(m => m.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(m => m.ThumbnailPath).HasMaxLength(1000);
        builder.Property(m => m.GedcomFileReference).HasMaxLength(1000);

        builder.Property(m => m.LatLongAcquiredFromExif).IsRequired();

        builder.HasOne(m => m.UploadedBy)
            .WithMany()
            .HasForeignKey(m => m.UploadedByPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Folder)
            .WithMany(m => m.Photos)
            .HasForeignKey(m => m.FolderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}