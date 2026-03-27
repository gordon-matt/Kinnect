namespace Kinnect.Services;

public class DocumentProcessingOptions
{
    /// <summary>
    /// Comma-separated list of allowed file extensions (including the leading dot).
    /// Defaults to PDF and common image types.
    /// </summary>
    public string AllowedExtensions { get; set; } = ".pdf,.jpg,.jpeg,.png,.gif,.webp";

    /// <summary>Maximum allowed file size in bytes. Defaults to 5 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 5_242_880;

    /// <summary>
    /// When true, uploaded images are passed through the same resize pipeline as photos
    /// (respecting ImageProcessing settings). Has no effect on PDF files.
    /// </summary>
    public bool AutoShrinkDocuments { get; set; } = true;

    public IReadOnlySet<string> AllowedExtensionSet =>
        AllowedExtensions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();
}