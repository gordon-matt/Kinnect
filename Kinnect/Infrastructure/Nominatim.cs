using System.Text.Json;

namespace Kinnect.Infrastructure;

public sealed record NominatimHit(double Latitude, double Longitude, string Label);

public static class Nominatim
{
    public static string FormatAddress(JsonElement address, bool includePostcode = true)
    {
        if (address.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        string place = First(address, "city", "town", "village", "hamlet", "suburb", "neighbourhood", "municipality");
        string country = Text(address, "country");
        bool isUk = country is "United Kingdom" or "Great Britain" or "England" or "Scotland" or "Wales" or "Northern Ireland";
        bool stateUseful = country is "United States" or "Canada" or "Australia";

        string? region = null;
        if (stateUseful && Text(address, "state") is { Length: > 0 } state)
        {
            region = state;
        }
        else if (isUk && Text(address, "county") is { Length: > 0 } county)
        {
            region = county;
        }
        else if (Text(address, "state") is { Length: > 0 } otherState)
        {
            region = otherState;
        }
        else if (Text(address, "county") is { Length: > 0 } otherCounty)
        {
            region = otherCounty;
        }

        if (region is not null && place.Length > 0 && string.Equals(region.Trim(), place.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            region = null;
        }

        var parts = new List<string>();
        string road = Text(address, "road");
        if (road.Length > 0)
        {
            string house = Text(address, "house_number");
            parts.Add(house.Length > 0 ? $"{house} {road}" : road);
        }

        if (place.Length > 0)
        {
            parts.Add(place);
        }

        if (region is not null)
        {
            parts.Add(region);
        }

        string postcode = Text(address, "postcode");
        if (includePostcode && postcode.Length > 0 && road.Length > 0)
        {
            parts.Add(postcode);
        }

        if (country.Length > 0)
        {
            parts.Add(country);
        }

        return string.Join(", ", parts);
    }

    private static string First(JsonElement address, params string[] names)
    {
        foreach (string name in names)
        {
            string value = Text(address, name);
            if (value.Length > 0)
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string Text(JsonElement address, string name) =>
        address.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
