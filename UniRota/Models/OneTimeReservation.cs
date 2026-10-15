namespace UniRota.Models;

public sealed record OneTimeReservation
{
    public string RequestId { get; init; } = string.Empty;

    public DateOnly Date { get; init; }

    public DayOfWeek DayOfWeek { get; init; }
}
