using System.Collections.Concurrent;
using Kinnect.Models.FamilyTree;

namespace Kinnect.Services;

/// <summary>Holds a finished export long enough for the browser to download it.</summary>
public sealed class GedcomExportStore
{
    private readonly ConcurrentDictionary<string, HeldFile> files = new();

    public string Put(GedcomExportFile file)
    {
        ForgetExpired();
        string token = Guid.NewGuid().ToString("N");
        files[token] = new HeldFile(file, DateTime.UtcNow.AddMinutes(15));
        return token;
    }

    public bool TryTake(string token, out GedcomExportFile? file)
    {
        file = null;
        if (!files.TryRemove(token, out var held) || held.ExpiresUtc < DateTime.UtcNow)
        {
            if (held is not null)
            {
                TryDelete(held.File.Path);
            }

            return false;
        }

        file = held.File;
        return true;
    }

    private void ForgetExpired()
    {
        foreach (var pair in files)
        {
            if (pair.Value.ExpiresUtc < DateTime.UtcNow && files.TryRemove(pair.Key, out var held))
            {
                TryDelete(held.File.Path);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            string? directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed record HeldFile(GedcomExportFile File, DateTime ExpiresUtc);
}
