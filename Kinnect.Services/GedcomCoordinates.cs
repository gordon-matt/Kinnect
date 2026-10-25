using System.Globalization;
using GeneGenie.Gedcom;

namespace Kinnect.Services;

/// <summary>
/// GEDCOM stores a map point as <c>N42.3601</c> / <c>W71.0589</c> under <c>PLAC</c> / <c>MAP</c>.
/// </summary>
internal static class GedcomCoordinates
{
    public static (double? Latitude, double? Longitude) Read(GedcomPlace? place)
    {
        if (place is null)
        {
            return (null, null);
        }

        double? latitude = Parse(place.Latitude, latitude: true);
        double? longitude = Parse(place.Longitude, latitude: false);
        return latitude is null || longitude is null ? (null, null) : (latitude, longitude);
    }

    public static string FormatLatitude(double latitude) => Format(latitude, positive: 'N', negative: 'S');

    public static string FormatLongitude(double longitude) => Format(longitude, positive: 'E', negative: 'W');

    private static string Format(double value, char positive, char negative)
    {
        char hemisphere = value < 0 ? negative : positive;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{hemisphere}{Math.Abs(value):0.######}");
    }

    private static double? Parse(string? text, bool latitude)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        int sign = 1;
        char head = text[0];
        if (head is 'S' or 's' or 'W' or 'w')
        {
            sign = -1;
            text = text[1..].Trim();
        }
        else if (head is 'N' or 'n' or 'E' or 'e')
        {
            text = text[1..].Trim();
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return null;
        }

        value *= sign;
        bool inRange = latitude ? value is >= -90 and <= 90 : value is >= -180 and <= 180;
        return inRange ? value : null;
    }
}
