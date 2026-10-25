using Hangfire;
using Kinnect.Data;

namespace Kinnect.Services.Jobs;

/// <summary>
/// Fills latitude and longitude on events that have a place name and no map point yet.
/// GEDCOM coordinates are stored at import. This covers the rest, including trees imported earlier.
/// </summary>
[HangfireSkipWhenPreviousInstanceIsRunningFilter]
[AutomaticRetry(Attempts = 0)]
public sealed class EventGeocodeJob(
    ApplicationDbContextBase database,
    NominatimPlaceLookup lookup,
    ILogger<EventGeocodeJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var pending = await database.PersonEvents
            .Where(evt => evt.Latitude == null && evt.Longitude == null && evt.Place != null && evt.Place != "")
            .ToListAsync(cancellationToken);

        var places = pending
            .GroupBy(evt => evt.Place!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length >= 3)
            .ToList();

        if (places.Count == 0)
        {
            return;
        }

        logger.LogInformation("Looking up coordinates for {Count} event places.", places.Count);
        int located = 0;

        foreach (var place in places)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await lookup.SearchAsync(place.Key, cancellationToken);
            if (result.Stop)
            {
                logger.LogWarning("Nominatim asked us to slow down. Event geocoding will continue on the next run.");
                break;
            }

            if (!result.Found)
            {
                continue;
            }

            foreach (var evt in place)
            {
                evt.Latitude = result.Latitude;
                evt.Longitude = result.Longitude;
            }

            located += place.Count();
            await database.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Stored coordinates on {Count} events.", located);
    }
}
