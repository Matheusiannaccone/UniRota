using System.Globalization;
using UniRota.Models;

namespace UniRota.ViewModels;

public sealed class ScheduledRouteDayViewModel
{
    public ScheduledRouteDayViewModel(WeeklyRoute route, DateOnly date)
    {
        Date = date;
        IsOfferedDay = route.DaysOfWeek.Contains(date.DayOfWeek);

        if (IsOfferedDay)
        {
            ScheduledPassengers = route.SeatCapacity!.Value
                - route.WeeklyAvailableSeats[date.DayOfWeek]
                + route.OneTimeReservations.Count(reservation => reservation.Date == date);
        }
    }

    public DateOnly Date { get; }

    public DayOfWeek DayOfWeek => Date.DayOfWeek;

    public string DisplayDay => RoutePresentationText.GetDayName(DayOfWeek)[..3];

    public string DisplayDate => Date.ToString("dd/MM", CultureInfo.InvariantCulture);

    public bool IsOfferedDay { get; }

    public int? ScheduledPassengers { get; }

    public string DisplayValue => ScheduledPassengers?.ToString(CultureInfo.InvariantCulture) ?? "-";

    public string AccessibilityText => IsOfferedDay
        ? $"{RoutePresentationText.GetDayName(DayOfWeek)}, {DisplayDate}: {DisplayValue} passageiros programados"
        : $"{RoutePresentationText.GetDayName(DayOfWeek)}, {DisplayDate}: rota não oferecida";
}
