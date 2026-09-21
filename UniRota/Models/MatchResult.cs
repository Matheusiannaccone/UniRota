namespace UniRota.Models;

public sealed record MatchResult(
    WeeklyRoute DriverRoute,
    IReadOnlyList<DayOfWeek> CompatibleDays,
    int TimeDifferenceMinutes,
    decimal BaseDistanceKm = 0m,
    decimal BaseDurationMinutes = 0m,
    decimal SharedDistanceKm = 0m,
    decimal SharedDurationMinutes = 0m,
    decimal DetourDistanceKm = 0m,
    decimal DetourDurationMinutes = 0m);
