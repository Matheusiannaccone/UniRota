namespace UniRota.Services;

public sealed class MatchingOptions
{
    public int MaximumDepartureTimeDifferenceMinutes { get; init; } = 30;

    public decimal MaximumDetourDistanceKm { get; init; } = 5m;

    public decimal MaximumDetourDurationMinutes { get; init; } = 15m;
}
