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
        return $"{GetShortAddress(route.Origin)} → "
               + GetShortAddress(route.Destination);
    }

    private static string GetShortAddress(string? address)
    {
        var normalizedAddress = address?.Trim() ?? string.Empty;

        if (normalizedAddress.Length == 0)
        {
            return string.Empty;
        }

        var components = normalizedAddress.Split(
            ',',
            StringSplitOptions.TrimEntries);

        if (components.Length >= 2
            && components[0].Length > 0)
        {
            var number = GetTextBeforeNeighborhood(components[1]);

            if (LooksLikeStreetNumber(number))
            {
                return $"{components[0]}, {number}";
            }
        }

        var neighborhoodSeparatorIndex = normalizedAddress.IndexOf(
            " - ",
            StringComparison.Ordinal);

        if (neighborhoodSeparatorIndex > 0)
        {
            var relevantPart = normalizedAddress[..neighborhoodSeparatorIndex]
                .Trim();

            if (relevantPart.Length > 0)
            {
                return relevantPart;
            }
        }

        return normalizedAddress;
    }

    private static string GetTextBeforeNeighborhood(string component)
    {
        var separatorIndex = component.IndexOf(
            " - ",
            StringComparison.Ordinal);

        return (separatorIndex >= 0
                ? component[..separatorIndex]
                : component)
            .Trim();
    }

    private static bool LooksLikeStreetNumber(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        return char.IsDigit(value[0])
            || value.Equals("s/n", StringComparison.OrdinalIgnoreCase)
            || value.Equals("sn", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("nº ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("n° ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("km ", StringComparison.OrdinalIgnoreCase);
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
