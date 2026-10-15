using System.Text.Json.Serialization;

namespace UniRota.Models;

public sealed class WeeklyRoute
{
    public string Id { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public RouteRole Role { get; init; }

    public string Origin { get; init; } = string.Empty;

    public string OriginPlaceId { get; init; } = string.Empty;

    public string Destination { get; init; } = string.Empty;

    public string DestinationPlaceId { get; init; } = string.Empty;

    public IReadOnlyList<DayOfWeek> DaysOfWeek { get; init; } = [];

    public int DepartureTimeMinutes { get; init; }

    public int? SeatCapacity { get; init; }

    public IReadOnlyDictionary<DayOfWeek, int> WeeklyAvailableSeats { get; init; }
        = new Dictionary<DayOfWeek, int>();

    public IReadOnlyList<OneTimeReservation> OneTimeReservations { get; init; } = [];

    // TEMPORÁRIO (Blocos 2–6): mantém os consumidores antigos compiláveis.
    // Não participa da persistência nem é derivado do novo schema.
    [JsonIgnore]
    public int? AvailableSeats { get; init; }

    public decimal EstimatedDistanceKm { get; init; }

    public long RequestRevision { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
