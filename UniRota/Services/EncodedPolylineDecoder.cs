using UniRota.Models;

namespace UniRota.Services;

public static class EncodedPolylineDecoder
{
    private const double CoordinateScale = 100000d;

    public static IReadOnlyList<MapCoordinate> Decode(string? encodedPolyline)
    {
        if (string.IsNullOrWhiteSpace(encodedPolyline))
        {
            return [];
        }

        var points = new List<MapCoordinate>();
        var index = 0;
        long latitude = 0;
        long longitude = 0;

        try
        {
            while (index < encodedPolyline.Length)
            {
                latitude = checked(
                    latitude + DecodeComponent(encodedPolyline, ref index));
                longitude = checked(
                    longitude + DecodeComponent(encodedPolyline, ref index));

                var point = new MapCoordinate(
                    latitude / CoordinateScale,
                    longitude / CoordinateScale);

                if (!point.IsValid)
                {
                    throw CreateInvalidPolylineException();
                }

                points.Add(point);
            }
        }
        catch (OverflowException exception)
        {
            throw CreateInvalidPolylineException(exception);
        }

        return points;
    }

    private static long DecodeComponent(string encodedPolyline, ref int index)
    {
        long value = 0;
        var shift = 0;

        while (true)
        {
            if (index >= encodedPolyline.Length || shift > 60)
            {
                throw CreateInvalidPolylineException();
            }

            var chunk = encodedPolyline[index++] - 63;

            if (chunk is < 0 or > 63)
            {
                throw CreateInvalidPolylineException();
            }

            value |= (long)(chunk & 0x1f) << shift;

            if ((chunk & 0x20) == 0)
            {
                break;
            }

            shift += 5;
        }

        return (value & 1) == 0
            ? value >> 1
            : ~(value >> 1);
    }

    private static FormatException CreateInvalidPolylineException(
        Exception? innerException = null)
    {
        return new FormatException(
            "A geometria do trajeto é inválida.",
            innerException);
    }
}
