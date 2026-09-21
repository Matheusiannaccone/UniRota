namespace UniRota.Models;

public sealed class MapRouteResult
{
    public long DistanceMeters { get; init; }

    public TimeSpan Duration { get; init; }

    public string EncodedPolyline { get; init; } = string.Empty;
}
