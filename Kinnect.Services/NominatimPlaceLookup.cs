using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Kinnect.Services;

/// <summary>
/// Turns a place name into a map point. Nominatim's <c>/search</c> does that;
/// <c>/reverse</c> goes the other way, from a point back to an address.
/// One request at a time, with a pause, which is what their usage policy asks for.
/// </summary>
public sealed class NominatimPlaceLookup(IHttpClientFactory httpClientFactory)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime nextRequestUtc = DateTime.MinValue;

    public async Task<NominatimSearch> SearchAsync(string place, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            TimeSpan wait = nextRequestUtc - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }

            nextRequestUtc = DateTime.UtcNow.AddSeconds(1.1);

            var client = httpClientFactory.CreateClient("nominatim");
            using var response = await client.GetAsync(
                "search?format=jsonv2&limit=1&q=" + Uri.EscapeDataString(place),
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                nextRequestUtc = DateTime.UtcNow.AddMinutes(1);
                return new NominatimSearch(false, true, 0, 0);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new NominatimSearch(false, false, 0, 0);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new NominatimSearch(false, false, 0, 0);
            }

            foreach (var hit in document.RootElement.EnumerateArray())
            {
                if (TryCoordinate(hit, "lat", out double latitude) && TryCoordinate(hit, "lon", out double longitude))
                {
                    return new NominatimSearch(true, false, latitude, longitude);
                }
            }

            return new NominatimSearch(false, false, 0, 0);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static bool TryCoordinate(JsonElement hit, string name, out double value)
    {
        value = 0;
        return hit.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
            && double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

/// <param name="Found">A point was returned.</param>
/// <param name="Stop">Nominatim asked us to back off. Do not send more queries in this run.</param>
public readonly record struct NominatimSearch(bool Found, bool Stop, double Latitude, double Longitude);
