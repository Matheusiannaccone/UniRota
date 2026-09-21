namespace UniRota.Models;

public readonly record struct MapCoordinate(
    double Latitude,
    double Longitude)
{
    public bool IsValid =>
        double.IsFinite(Latitude)
        && double.IsFinite(Longitude)
        && Latitude is >= -90d and <= 90d
        && Longitude is >= -180d and <= 180d;
}
