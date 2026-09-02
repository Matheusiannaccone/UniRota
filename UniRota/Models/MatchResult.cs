namespace UniRota.Models;

public sealed record MatchResult(
    WeeklyRoute DriverRoute,
    IReadOnlyList<DayOfWeek> CompatibleDays,
    int TimeDifferenceMinutes);
