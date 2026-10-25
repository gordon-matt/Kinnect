namespace Kinnect.Models;

/// <summary>
/// Bio and notes are one field. Older rows and backups still have both, and this is how they are joined.
/// </summary>
public static class PersonNoteText
{
    public const int MaxLength = 4000;

    public static string? Merge(string? bio, string? note)
    {
        bool hasBio = !string.IsNullOrWhiteSpace(bio);
        bool hasNote = !string.IsNullOrWhiteSpace(note);
        string? merged = (hasBio, hasNote) switch
        {
            (true, false) => bio!.Trim(),
            (false, true) => note!.Trim(),
            (true, true) => $"Bio\n{bio!.Trim()}\n\nNotes\n{note!.Trim()}",
            _ => null,
        };

        if (merged is null || merged.Length <= MaxLength)
        {
            return merged;
        }

        return merged[..MaxLength];
    }
}
