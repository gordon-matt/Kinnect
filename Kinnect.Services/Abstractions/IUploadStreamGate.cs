namespace Kinnect.Services.Abstractions;

/// <summary>
/// Every upload the site is currently sending. Delete revokes those reads, including other
/// people's, so the file is not left locked by a viewer who still has it open.
/// </summary>
public interface IUploadStreamGate
{
    /// <summary>Opens the file for one response. Throws <see cref="IOException"/> when a delete is in progress.</summary>
    Stream OpenRead(string fullPath);

    /// <summary>
    /// Stops every active read of <paramref name="fullPath"/> and refuses new ones until disposed.
    /// </summary>
    UploadRevocation Revoke(string fullPath);
}

/// <summary>A delete in progress. Disposing it allows the path to be read again.</summary>
public sealed class UploadRevocation : IDisposable
{
    private readonly Action end;
    private int ended;

    public UploadRevocation(int closedReads, Action end)
    {
        ClosedReads = closedReads;
        this.end = end;
    }

    public int ClosedReads { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref ended, 1) == 0)
        {
            end();
        }
    }
}
