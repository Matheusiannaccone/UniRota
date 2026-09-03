using UniRota.Models;

namespace UniRota.ViewModels;

internal static class RoutePresentationText
{
    public static string GetRoleName(RouteRole role)
    {
        return role switch
        {
            RouteRole.Driver => "Motorista",
            RouteRole.Passenger => "Passageiro",
            _ => string.Empty
        };
    }

    public static string GetDayName(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => "Segunda-feira",
            DayOfWeek.Tuesday => "Terça-feira",
            DayOfWeek.Wednesday => "Quarta-feira",
            DayOfWeek.Thursday => "Quinta-feira",
            DayOfWeek.Friday => "Sexta-feira",
            DayOfWeek.Saturday => "Sábado",
            DayOfWeek.Sunday => "Domingo",
            _ => string.Empty
        };
    }

    public static string GetOriginDestinationText(WeeklyRoute route)
    {
        return $"{route.Origin} → {route.Destination}";
    }

    public static string GetDaysText(IEnumerable<DayOfWeek> days)
    {
        return string.Join(", ", days.Select(GetDayName));
    }

    public static string GetDepartureTimeText(int departureTimeMinutes)
    {
        var departureTime = TimeSpan.FromMinutes(departureTimeMinutes);
        return $"{(int)departureTime.TotalHours:00}:{departureTime.Minutes:00}";
    }

    public static string GetAvailableSeatsText(int? availableSeats)
    {
        return availableSeats switch
        {
            1 => "1 vaga",
            > 1 => $"{availableSeats} vagas",
            _ => string.Empty
        };
    }
}
