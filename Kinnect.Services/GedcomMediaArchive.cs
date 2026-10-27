using System.IO.Compression;

namespace Kinnect.Services;

/// <summary>
/// A zip upload holding a GEDCOM and the media files it refers to. Entries are only ever read as streams, never
/// extracted by their own names, so a hostile entry name such as <c>../../x</c> cannot write outside the work folder.
/// </summary>
internal sealed class GedcomMediaArchive : IDisposable
{
    private static readonly byte[] zipSignature = [0x50, 0x4B, 0x03, 0x04];

    private readonly ZipArchive zip;
    private readonly Dictionary<string, ZipArchiveEntry> entries;

    private GedcomMediaArchive(ZipArchive zip, Dictionary<string, ZipArchiveEntry> entries)
    {
        this.zip = zip;
        this.entries = entries;

        GedcomEntryName = entries.Keys
            .Where(name => name.EndsWith(".ged", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".gedcom", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name.Count(c => c is '/' or '\\'))
            .ThenByDescending(name => entries[name].Length)
            .FirstOrDefault();

        Locator = new GedcomMediaLocator(entries.Keys, GedcomEntryName ?? string.Empty);
    }

    /// <summary>The .ged inside the zip. If there are several, the one nearest the root (then the largest) wins.</summary>
    public string? GedcomEntryName { get; }

    public GedcomMediaLocator Locator { get; }

    public static bool IsZip(string path)
    {
        using var file = File.OpenRead(path);
        byte[] header = new byte[zipSignature.Length];
        return file.Read(header) == header.Length && header.AsSpan().SequenceEqual(zipSignature);
    }

    public static GedcomMediaArchive Open(string path)
    {
        var zip = ZipFile.OpenRead(path);
        var entries = zip.Entries
            .Where(entry => entry.Name.Length > 0
                && !entry.FullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)
                && !entry.Name.StartsWith("._", StringComparison.Ordinal))
            .GroupBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return new GedcomMediaArchive(zip, entries);
    }

    public long GetLength(string entryName) => entries[entryName].Length;

    public Stream OpenEntry(string entryName) => entries[entryName].Open();

    public void ExtractGedcom(string destinationPath) => entries[GedcomEntryName!].ExtractToFile(destinationPath, overwrite: true);

    public void Dispose() => zip.Dispose();
}