namespace UniRota.Models;

public sealed class RideRequest
{
    public string Id { get; init; } = string.Empty;

    public string PassengerUserId { get; init; } = string.Empty;

    public string PassengerUserName { get; init; } = string.Empty;

    public string DriverUserId { get; init; } = string.Empty;

    public string DriverUserName { get; init; } = string.Empty;

    public string PassengerRouteId { get; init; } = string.Empty;

    public string DriverRouteId { get; init; } = string.Empty;

    public IReadOnlyList<DayOfWeek> CompatibleDays { get; init; } = [];

    public RideRequestType Type { get; init; }

    public RideRequestStatus Status { get; init; }

    public DateOnly? RequestedDate { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
