using Kinnect.Services;
using Microsoft.AspNetCore.StaticFiles;

namespace Kinnect.Infrastructure;

/// <summary>
/// Sends files under the uploads root. Reads go through <see cref="IUploadStreamGate"/>, so a delete
/// can stop every viewer and then remove the file. Range requests are honored so a video can seek.
/// </summary>
public static class UploadFilesMiddleware
{
    public static void UseUploadFiles(this WebApplication app, string root, IUploadStreamGate gate)
    {
        string rootFull = Path.GetFullPath(root);
        var contentTypes = new FileExtensionContentTypeProvider();

        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/uploads", out PathString rest))
            {
                await next(context);
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            string relative = (rest.Value ?? string.Empty).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(rootFull, relative));
            if (!IsInside(rootFull, full) || !File.Exists(full))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (!contentTypes.TryGetContentType(full, out string? contentType))
            {
                contentType = "application/octet-stream";
            }

            Stream stream;
            try
            {
                stream = gate.OpenRead(full);
            }
            catch (IOException)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await using (stream)
            {
                long length = new FileInfo(full).Length;
                long start = 0;
                long end = length - 1;
                context.Response.ContentType = contentType;
                context.Response.Headers.AcceptRanges = "bytes";

                if (context.Request.Headers.Range.Count > 0)
                {
                    if (!TryParseRange(context.Request.Headers.Range.ToString(), length, out start, out end))
                    {
                        context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                        context.Response.Headers.ContentRange = $"bytes */{length}";
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status206PartialContent;
                    context.Response.Headers.ContentRange = $"bytes {start}-{end}/{length}";
                }

                long count = length == 0 ? 0 : end - start + 1;
                context.Response.ContentLength = count;
                if (HttpMethods.IsHead(context.Request.Method) || count == 0)
                {
                    return;
                }

                try
                {
                    stream.Seek(start, SeekOrigin.Begin);
                    await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
                }
                catch (Exception) when (stream is IRevocableRead { Revoked: true } || context.RequestAborted.IsCancellationRequested)
                {
                }

                if (stream is IRevocableRead { Revoked: true })
                {
                    context.Abort();
                }
            }
        });
    }

    private static bool IsInside(string root, string full)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.Equals(root, comparison) || full.StartsWith(prefix, comparison);
    }

    private static bool TryParseRange(string header, long length, out long start, out long end)
    {
        start = 0;
        end = length - 1;
        if (length <= 0 || !header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string spec = header["bytes=".Length..].Split(',')[0].Trim();
        int dash = spec.IndexOf('-');
        if (dash < 0)
        {
            return false;
        }

        string left = spec[..dash];
        string right = spec[(dash + 1)..];
        if (left.Length == 0)
        {
            if (!long.TryParse(right, out long suffix) || suffix <= 0)
            {
                return false;
            }

            if (suffix > length)
            {
                suffix = length;
            }

            start = length - suffix;
            return true;
        }

        if (!long.TryParse(left, out start) || start < 0 || start >= length)
        {
            return false;
        }

        if (right.Length == 0)
        {
            end = length - 1;
            return true;
        }

        if (!long.TryParse(right, out end) || end < start)
        {
            return false;
        }

        if (end >= length)
        {
            end = length - 1;
        }

        return true;
    }
}
