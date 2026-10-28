namespace Kinnect.Services;

/// <summary>
/// Tracks each upload being sent to a browser. A delete closes every one of those streams
/// before removing the file, so a viewer — or several — cannot keep the file locked.
/// </summary>
public sealed class UploadStreamGate : IUploadStreamGate
{
    private readonly object sync = new();
    private readonly Dictionary<string, PathLease> leases = new(PathComparer);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public Stream OpenRead(string fullPath)
    {
        string key = Path.GetFullPath(fullPath);

        lock (sync)
        {
            if (!leases.TryGetValue(key, out PathLease? lease))
            {
                lease = new PathLease();
                leases[key] = lease;
            }

            if (lease.Revoking)
            {
                throw new IOException($"'{key}' is being deleted.");
            }

            FileStream stream;
            try
            {
                stream = new FileStream(
                    key,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 1,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
            }
            catch
            {
                if (lease.Streams.Count == 0)
                {
                    leases.Remove(key);
                }

                throw;
            }

            TrackedStream? tracked = null;
            tracked = new TrackedStream(stream, () => Release(key, tracked!));
            lease.Streams.Add(tracked);
            return tracked;
        }
    }

    public UploadRevocation Revoke(string fullPath)
    {
        string key = Path.GetFullPath(fullPath);
        TrackedStream[] open;

        lock (sync)
        {
            if (!leases.TryGetValue(key, out PathLease? lease))
            {
                lease = new PathLease();
                leases[key] = lease;
            }

            lease.Revoking = true;
            open = lease.Streams.ToArray();
            lease.Streams.Clear();
        }

        foreach (TrackedStream stream in open)
        {
            stream.Revoke();
        }

        return new UploadRevocation(open.Length, () => EndRevoke(key));
    }

    private void EndRevoke(string key)
    {
        lock (sync)
        {
            if (!leases.TryGetValue(key, out PathLease? lease))
            {
                return;
            }

            lease.Revoking = false;
            if (lease.Streams.Count == 0)
            {
                leases.Remove(key);
            }
        }
    }

    private void Release(string key, TrackedStream stream)
    {
        lock (sync)
        {
            if (!leases.TryGetValue(key, out PathLease? lease))
            {
                return;
            }

            lease.Streams.Remove(stream);
            if (!lease.Revoking && lease.Streams.Count == 0)
            {
                leases.Remove(key);
            }
        }
    }

    private sealed class PathLease
    {
        public bool Revoking { get; set; }

        public HashSet<TrackedStream> Streams { get; } = [];
    }

    private sealed class TrackedStream(FileStream inner, Action onDispose) : Stream, IRevocableRead
    {
        private int revoked;
        private int disposed;

        public bool Revoked => Volatile.Read(ref revoked) == 1;

        public void Revoke()
        {
            Interlocked.Exchange(ref revoked, 1);
            try
            {
                inner.Dispose();
            }
            catch (IOException)
            {
                // The read that was in progress observes the closed handle and ends.
            }
        }

        public override bool CanRead => !Revoked && inner.CanRead;

        public override bool CanSeek => !Revoked && inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Revoked)
            {
                return 0;
            }

            try
            {
                return inner.Read(buffer, offset, count);
            }
            catch (Exception ex) when (Revoked && ex is (ObjectDisposedException or IOException or OperationCanceledException))
            {
                return 0;
            }
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Revoked)
            {
                return ValueTask.FromResult(0);
            }

            return ReadRevokedAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!disposing || Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            try
            {
                inner.Dispose();
            }
            catch (IOException)
            {
            }

            onDispose();
        }

        private async ValueTask<int> ReadRevokedAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            try
            {
                return await inner.ReadAsync(buffer, cancellationToken);
            }
            catch (Exception ex) when (Revoked && ex is (ObjectDisposedException or IOException or OperationCanceledException))
            {
                return 0;
            }
        }
    }
}

/// <summary>A stream the upload gate can cut off while it is being sent.</summary>
public interface IRevocableRead
{
    bool Revoked { get; }
}
