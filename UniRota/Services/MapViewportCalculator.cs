using UniRota.Models;

namespace UniRota.Services;

public static class MapViewportCalculator
{
    private const double DefaultPaddingFactor = 1.25d;
    private const double MinimumSpanDegrees = 0.01d;

    public static MapViewport Calculate(
        IReadOnlyList<MapCoordinate> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count == 0)
        {
            throw new ArgumentException(
                "Informe ao menos um ponto para calcular a região do mapa.",
                nameof(points));
        }

        if (points.Any(point => !point.IsValid))
        {
            throw new ArgumentException(
                "A região do mapa contém coordenadas inválidas.",
                nameof(points));
        }

        var minimumLatitude = points.Min(point => point.Latitude);
        var maximumLatitude = points.Max(point => point.Latitude);
        var latitudeSpan = Math.Max(
            MinimumSpanDegrees,
            (maximumLatitude - minimumLatitude) * DefaultPaddingFactor);
        var (centerLongitude, longitudeSpan) = CalculateLongitudeBounds(points);

        return new MapViewport(
            new MapCoordinate(
                (minimumLatitude + maximumLatitude) / 2d,
                centerLongitude),
            Math.Min(180d, latitudeSpan),
            Math.Min(
                360d,
                Math.Max(
                    MinimumSpanDegrees,
                    longitudeSpan * DefaultPaddingFactor)));
    }

    private static (double Center, double Span) CalculateLongitudeBounds(
        IReadOnlyList<MapCoordinate> points)
    {
        if (points.Count == 1)
        {
            return (points[0].Longitude, 0d);
        }

        var longitudes = points
            .Select(point => NormalizeToPositiveLongitude(point.Longitude))
            .OrderBy(longitude => longitude)
            .ToArray();
        var largestGap = double.NegativeInfinity;
        var arcStart = longitudes[0];

        for (var index = 0; index < longitudes.Length; index++)
        {
            var nextIndex = (index + 1) % longitudes.Length;
            var nextLongitude = nextIndex == 0
                ? longitudes[0] + 360d
                : longitudes[nextIndex];
            var gap = nextLongitude - longitudes[index];

            if (gap > largestGap)
            {
                largestGap = gap;
                arcStart = nextLongitude % 360d;
            }
        }

        var span = 360d - largestGap;
        var center = (arcStart + (span / 2d)) % 360d;

        return (NormalizeLongitude(center), span);
    }

    private static double NormalizeToPositiveLongitude(double longitude)
    {
        return longitude < 0d ? longitude + 360d : longitude;
    }

    private static double NormalizeLongitude(double longitude)
    {
        return longitude > 180d ? longitude - 360d : longitude;
    }
}
