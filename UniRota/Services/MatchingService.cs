using System.Globalization;
using System.Text;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.Services;

public sealed class MatchingService : IMatchingService
{
    private const int MaximumTimeDifferenceMinutes = 30;

    public IReadOnlyList<MatchResult> FindMatches(
        WeeklyRoute passengerRoute,
        IEnumerable<WeeklyRoute> candidateRoutes)
    {
        ArgumentNullException.ThrowIfNull(passengerRoute);
        ArgumentNullException.ThrowIfNull(candidateRoutes);

        ValidatePassengerRoute(passengerRoute);

        var passengerOrigin = NormalizeLocation(passengerRoute.Origin);
        var passengerDestination = NormalizeLocation(passengerRoute.Destination);
        var passengerDays = passengerRoute.DaysOfWeek.ToHashSet();
        var matches = new List<MatchResult>();

        foreach (var driverRoute in candidateRoutes)
        {
            // Invalid historical candidates are ignored so one malformed route does
            // not prevent the remaining valid routes from being evaluated.
            if (!IsValidDriverCandidate(driverRoute)
                || string.Equals(
                    passengerRoute.UserId,
                    driverRoute.UserId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    passengerOrigin,
                    NormalizeLocation(driverRoute.Origin),
                    StringComparison.Ordinal)
                || !string.Equals(
                    passengerDestination,
                    NormalizeLocation(driverRoute.Destination),
                    StringComparison.Ordinal))
            {
                continue;
            }

            var compatibleDays = driverRoute.DaysOfWeek
                .Where(passengerDays.Contains)
                .Distinct()
                .OrderBy(day => day)
                .ToArray();

            if (compatibleDays.Length == 0)
            {
                continue;
            }

            var timeDifferenceMinutes = Math.Abs(
                passengerRoute.DepartureTimeMinutes
                - driverRoute.DepartureTimeMinutes);

            if (timeDifferenceMinutes > MaximumTimeDifferenceMinutes)
            {
                continue;
            }

            matches.Add(new MatchResult(
                driverRoute,
                compatibleDays,
                timeDifferenceMinutes));
        }

        return matches
            .OrderBy(result => result.TimeDifferenceMinutes)
            .ThenByDescending(result => result.CompatibleDays.Count)
            .ThenBy(result => result.DriverRoute.DepartureTimeMinutes)
            .ThenBy(
                result => result.DriverRoute.Id ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidatePassengerRoute(WeeklyRoute passengerRoute)
    {
        if (passengerRoute.Role != RouteRole.Passenger)
        {
            throw new ArgumentException(
                "A rota de referência deve possuir o papel de passageiro.",
                nameof(passengerRoute));
        }

        if (string.IsNullOrWhiteSpace(passengerRoute.UserId))
        {
            throw new ArgumentException(
                "A rota de referência deve possuir um identificador de usuário.",
                nameof(passengerRoute));
        }

        if (string.IsNullOrWhiteSpace(passengerRoute.Origin))
        {
            throw new ArgumentException(
                "A rota de referência deve possuir uma origem.",
                nameof(passengerRoute));
        }

        if (string.IsNullOrWhiteSpace(passengerRoute.Destination))
        {
            throw new ArgumentException(
                "A rota de referência deve possuir um destino.",
                nameof(passengerRoute));
        }

        if (passengerRoute.DaysOfWeek is null
            || passengerRoute.DaysOfWeek.Count == 0
            || passengerRoute.DaysOfWeek.Any(
                day => !Enum.IsDefined(typeof(DayOfWeek), day)))
        {
            throw new ArgumentException(
                "A rota de referência deve possuir ao menos um dia válido.",
                nameof(passengerRoute));
        }

        if (passengerRoute.DepartureTimeMinutes is < 0 or > 1439)
        {
            throw new ArgumentOutOfRangeException(
                nameof(passengerRoute),
                passengerRoute.DepartureTimeMinutes,
                "O horário da rota de referência deve estar entre 0 e 1439 minutos.");
        }
    }

    private static bool IsValidDriverCandidate(WeeklyRoute? route)
    {
        return route is not null
            && route.Role == RouteRole.Driver
            && !string.IsNullOrWhiteSpace(route.UserId)
            && !string.IsNullOrWhiteSpace(route.Origin)
            && !string.IsNullOrWhiteSpace(route.Destination)
            && route.DaysOfWeek is not null
            && route.DaysOfWeek.Count > 0
            && route.DaysOfWeek.All(
                day => Enum.IsDefined(typeof(DayOfWeek), day))
            && route.DepartureTimeMinutes is >= 0 and <= 1439
            && route.AvailableSeats is > 0;
    }

    private static string NormalizeLocation(string value)
    {
        var decomposedValue = value.Normalize(NormalizationForm.FormD);
        var normalizedValue = new StringBuilder(decomposedValue.Length);
        var hasPendingSpace = false;

        foreach (var character in decomposedValue)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);

            if (unicodeCategory is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                hasPendingSpace = normalizedValue.Length > 0;
                continue;
            }

            if (hasPendingSpace)
            {
                normalizedValue.Append(' ');
                hasPendingSpace = false;
            }

            normalizedValue.Append(character);
        }

        return normalizedValue
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();
    }
}
