namespace UniRota.Models;

public sealed class WeeklyRoute
{
    public string Id { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public RouteRole Role { get; init; }

    public string Origin { get; init; } = string.Empty;

    public string Destination { get; init; } = string.Empty;

    public IReadOnlyList<DayOfWeek> DaysOfWeek { get; init; } = [];

    public int DepartureTimeMinutes { get; init; }

    public int? AvailableSeats { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
