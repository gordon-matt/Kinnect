using System.Globalization;
using GedcomSharp.Model;

namespace Kinnect.Services;

/// <summary>
/// GEDCOM stores a map point as <c>N42.3601</c> / <c>W71.0589</c> under <c>PLAC</c> / <c>MAP</c>.
/// </summary>
internal static class GedcomCoordinates
{
    public static (double? Latitude, double? Longitude) Read(Place? place) =>
        place?.Latitude is double latitude && place.Longitude is double longitude ? (latitude, longitude) : (null, null);

    public static string FormatLatitude(double latitude) => Format(latitude, positive: 'N', negative: 'S');

    public static string FormatLongitude(double longitude) => Format(longitude, positive: 'E', negative: 'W');

    private static string Format(double value, char positive, char negative)
    {
        char hemisphere = value < 0 ? negative : positive;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{hemisphere}{Math.Abs(value):0.######}");
    }
}
