using System.Text;

namespace Kinnect.Services;

public enum GedcomMediaLookupStatus
{
    Found,

    /// <summary>A web address or e-mail address. Kinnect never fetches these.</summary>
    Remote,

    /// <summary>The value is empty or only names a folder (some exporters write <c>C:\Photos\</c>).</summary>
    NoFileName,

    /// <summary>A file name, but nothing with that name was uploaded.</summary>
    Missing,
}

public readonly record struct GedcomMediaLookupResult(GedcomMediaLookupStatus Status, string? EntryName = null);

/// <summary>
/// Works out which uploaded file a GEDCOM <c>FILE</c> value refers to. Exporters write whatever path was on the
/// original computer (<c>photos/a.jpg</c>, <c>C:\Users\Jo\Pictures\a.jpg</c>, <c>./a.jpg</c>, <c>file:///...</c>), but the
/// zip only contains the folders the user chose to include. So an exact match is tried first, then the same
/// path next to the .ged, then the longest tail of the path that exists anywhere in the zip.
/// </summary>
public sealed class GedcomMediaLocator
{
    private static readonly string[] remoteSchemes = ["http://", "https://", "ftp://", "ftps://", "mailto:"];

    private readonly Dictionary<string, string> namesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> namesByFileName = new(StringComparer.OrdinalIgnoreCase);
    private readonly string gedcomDirectory;

    /// <param name="entryNames">Paths of the files in the upload, as they appear in the zip.</param>
    /// <param name="gedcomEntryName">Path of the .ged inside the zip. Relative references are resolved from its folder.</param>
    public GedcomMediaLocator(IEnumerable<string> entryNames, string gedcomEntryName)
    {
        gedcomDirectory = Normalize(GetDirectory(Normalize(gedcomEntryName)));

        foreach (string name in entryNames)
        {
            // Directory entries in a zip end with a slash.
            string path = Normalize(name);
            if (path.Length == 0 || name.EndsWith('/') || name.EndsWith('\\'))
            {
                continue;
            }

            namesByPath.TryAdd(path, name);

            string fileName = path[(path.LastIndexOf('/') + 1)..];
            if (!namesByFileName.TryGetValue(fileName, out var list))
            {
                namesByFileName[fileName] = list = [];
            }

            list.Add(path);
        }
    }

    public int FileCount => namesByPath.Count;

    public GedcomMediaLookupResult Resolve(string? reference, string? format = null)
    {
        string? value = reference?.Trim().Trim('"');
        if (string.IsNullOrEmpty(value))
        {
            return new(GedcomMediaLookupStatus.NoFileName);
        }

        if (string.Equals(format, "URL", StringComparison.OrdinalIgnoreCase)
            || remoteSchemes.Any(scheme => value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
        {
            return new(GedcomMediaLookupStatus.Remote);
        }

        if (value.EndsWith('/') || value.EndsWith('\\') || ToSegments(value).Length == 0)
        {
            return new(GedcomMediaLookupStatus.NoFileName);
        }

        // Percent-encoding only appears in file: URIs, but a real file can also contain a literal '%', so try both.
        foreach (string candidate in Variants(value))
        {
            string? found = FindByPath(ToSegments(candidate));
            if (found is not null)
            {
                return new(GedcomMediaLookupStatus.Found, found);
            }
        }

        return new(GedcomMediaLookupStatus.Missing);
    }

    private string? FindByPath(string[] segments)
    {
        if (segments.Length == 0)
        {
            return null;
        }

        string full = string.Join('/', segments);

        if (namesByPath.TryGetValue(full, out string? exact))
        {
            return exact;
        }

        if (gedcomDirectory.Length > 0 && namesByPath.TryGetValue($"{gedcomDirectory}/{full}", out string? beside))
        {
            return beside;
        }

        // Longest tail first, so "C:/Pics/Kennedy/a.jpg" prefers "Kennedy/a.jpg" over any other "a.jpg".
        for (int start = 1; start < segments.Length; start++)
        {
            string tail = string.Join('/', segments[start..]);
            string? match = FindByTail(segments[^1], tail);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private string? FindByTail(string fileName, string tail)
    {
        if (!namesByFileName.TryGetValue(fileName, out var candidates))
        {
            return null;
        }

        string suffix = "/" + tail;
        var matches = candidates
            .Where(path => path.Equals(tail, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        // Several files can share a name in different folders. Prefer the one nearest the .ged, then the shallowest.
        string prefix = gedcomDirectory.Length == 0 ? string.Empty : gedcomDirectory + "/";
        string best = matches
            .OrderByDescending(path => prefix.Length > 0 && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ThenBy(path => path.Count(c => c == '/'))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .First();

        return namesByPath[best];
    }

    private static IEnumerable<string> Variants(string value)
    {
        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["file:".Length..].TrimStart('/');
        }

        yield return value;

        if (value.Contains('%'))
        {
            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(value);
            }
            catch (UriFormatException)
            {
                yield break;
            }

            if (!string.Equals(decoded, value, StringComparison.Ordinal))
            {
                yield return decoded;
            }
        }
    }

    /// <summary>Splits into path segments, dropping a drive letter (<c>C:</c>) and any <c>.</c> or <c>..</c> segments.</summary>
    private static string[] ToSegments(string value) =>
    [
        .. Normalize(value)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment is not ("." or "..") && !(segment.Length == 2 && segment[1] == ':')),
    ];

    private static string Normalize(string value) =>
        value.Replace('\\', '/').Trim('/').Normalize(NormalizationForm.FormC);

    private static string GetDirectory(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }
}
