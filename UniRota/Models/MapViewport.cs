namespace UniRota.Models;

public sealed record MapViewport(
    MapCoordinate Center,
    double LatitudeDegrees,
    double LongitudeDegrees);
