using UniRota.Models;

namespace UniRota.Services;

public static class RideRequestRules
{
    public const string PassengerDaysUnavailableMessage =
        "Os dias dessa solicitação possuem conflito com caronas já confirmadas do passageiro.";

    public const string ConfirmedDaysChangedMessage =
        "Os dias disponíveis para aceite foram alterados. Atualize a lista e confirme novamente.";

    public static IReadOnlyList<DayOfWeek> GetPassengerAvailableWeeklyDays(
        WeeklyRoute passengerRoute, IReadOnlyList<DayOfWeek> compatibleDays, DateOnly today) =>
        compatibleDays.Intersect(passengerRoute.DaysOfWeek)
            .Except(passengerRoute.WeeklyAcceptedDays)
            .Except(passengerRoute.OneTimeAcceptedDates.Where(date => date >= today).Select(date => date.DayOfWeek))
            .OrderBy(day => day).ToArray();

    public const string PassengerOnceConflictMessage =
        "O passageiro já possui uma carona confirmada para esta data.";

    public const string PassengerWeeklyConflictMessage =
        "O passageiro já possui uma carona semanal confirmada para este dia.";

    public static string? GetPassengerOnceConflict(WeeklyRoute passengerRoute, DateOnly requestedDate, DateOnly today)
    {
        if (requestedDate < today) return "A data da carona não pode estar no passado.";
        if (passengerRoute.WeeklyAcceptedDays.Contains(requestedDate.DayOfWeek)) return PassengerWeeklyConflictMessage;
        return passengerRoute.OneTimeAcceptedDates.Contains(requestedDate) ? PassengerOnceConflictMessage : null;
    }

    public static bool CanPassengerAcceptOnce(WeeklyRoute passengerRoute, DateOnly requestedDate, DateOnly today) =>
        GetPassengerOnceConflict(passengerRoute, requestedDate, today) is null;

    public static void EnsurePending(RideRequestStatus status)
    {
        if (status != RideRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                "Esta solicitação já foi processada.");
        }
    }

    public static bool HasWeeklySeats(
        IReadOnlyDictionary<DayOfWeek, int> weeklySeats,
        IReadOnlyList<DayOfWeek> days) =>
        days.Any(day => weeklySeats.TryGetValue(day, out var seats) && seats > 0);

    public static bool HasSeatsOnDate(
        IReadOnlyDictionary<DayOfWeek, int> weeklySeats,
        IReadOnlyList<OneTimeReservation> reservations,
        DateOnly date) =>
        weeklySeats.TryGetValue(date.DayOfWeek, out var seats)
        && seats - reservations.Count(reservation => reservation.Date == date) > 0;

    public static IReadOnlyList<DayOfWeek> GetAcceptableWeeklyDays(
        IReadOnlyDictionary<DayOfWeek, int> weeklySeats,
        IReadOnlyList<OneTimeReservation> reservations,
        IReadOnlyList<DayOfWeek> compatibleDays,
        DateOnly today) =>
        compatibleDays.Distinct()
            .Where(day => weeklySeats.TryGetValue(day, out var seats) && seats > 0
                && reservations.Where(reservation => reservation.Date >= today
                        && reservation.Date.DayOfWeek == day)
                    .GroupBy(reservation => reservation.Date)
                    .All(group => group.Count() < seats))
            .OrderBy(day => day)
            .ToArray();

    public static bool CanAccept(RideRequest request, WeeklyRoute route, DateOnly today)
    {
        if (request.Status != RideRequestStatus.Pending || route.Role != RouteRole.Driver
            || request.DriverRouteId != route.Id || request.DriverUserId != route.UserId)
        {
            return false;
        }

        return request.Type switch
        {
            RideRequestType.Weekly => GetAcceptableWeeklyDays(route.WeeklyAvailableSeats,
                route.OneTimeReservations,
                request.CompatibleDays.Where(route.DaysOfWeek.Contains).ToArray(), today).Count > 0,
            RideRequestType.Once => request.RequestedDate is { } date && date >= today
                && route.DaysOfWeek.Contains(date.DayOfWeek)
                && !route.OneTimeReservations.Any(reservation => reservation.RequestId == request.Id)
                && HasSeatsOnDate(route.WeeklyAvailableSeats, route.OneTimeReservations, date),
            _ => false
        };
    }

    public static (IReadOnlyDictionary<DayOfWeek, int> WeeklySeats,
        IReadOnlyList<DayOfWeek> AcceptedDays) ApplyWeeklyAcceptance(
        IReadOnlyDictionary<DayOfWeek, int> weeklySeats,
        IReadOnlyList<OneTimeReservation> reservations,
        IReadOnlyList<DayOfWeek> days,
        DateOnly today)
    {
        var acceptedDays = GetAcceptableWeeklyDays(weeklySeats, reservations, days, today);
        if (acceptedDays.Count == 0)
        {
            throw new InvalidOperationException("Não há vagas disponíveis nos dias solicitados.");
        }

        var updated = weeklySeats.ToDictionary(entry => entry.Key, entry => entry.Value);
        foreach (var day in acceptedDays)
        {
            updated[day]--;
        }

        return (updated, acceptedDays);
    }

    public static IReadOnlyList<OneTimeReservation> ApplyOnceAcceptance(
        IReadOnlyDictionary<DayOfWeek, int> weeklySeats,
        IReadOnlyList<OneTimeReservation> reservations,
        string requestId,
        DateOnly date,
        DateOnly today)
    {
        if (date < today)
        {
            throw new InvalidOperationException("A data da carona não pode estar no passado.");
        }

        if (reservations.Any(reservation => reservation.RequestId == requestId))
        {
            throw new InvalidOperationException("Esta solicitação já possui reserva pontual.");
        }

        if (!HasSeatsOnDate(weeklySeats, reservations, date))
        {
            throw new InvalidOperationException("Não há vagas disponíveis para a data solicitada.");
        }

        return [.. reservations, new OneTimeReservation
        {
            RequestId = requestId,
            Date = date,
            DayOfWeek = date.DayOfWeek
        }];
    }

    public static IReadOnlyList<DayOfWeek> ValidateForCreation(
        RideRequestType type,
        IEnumerable<DayOfWeek> compatibleDays,
        DateOnly? requestedDate,
        DateOnly currentDate)
    {
        if (!Enum.IsDefined(typeof(RideRequestType), type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                "Selecione um tipo válido de solicitação.");
        }

        ArgumentNullException.ThrowIfNull(compatibleDays);

        var normalizedDays = compatibleDays
            .Distinct()
            .OrderBy(day => day)
            .ToArray();

        if (normalizedDays.Length == 0
            || normalizedDays.Any(
                day => !Enum.IsDefined(typeof(DayOfWeek), day)))
        {
            throw new ArgumentException(
                "A solicitação deve possuir ao menos um dia compatível válido.",
                nameof(compatibleDays));
        }

        if (type == RideRequestType.Once)
        {
            if (requestedDate is null)
            {
                throw new ArgumentException(
                    "Selecione a data da carona.",
                    nameof(requestedDate));
            }

            if (requestedDate.Value < currentDate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedDate),
                    "A data da carona não pode estar no passado.");
            }

            if (!normalizedDays.Contains(requestedDate.Value.DayOfWeek))
            {
                throw new ArgumentException(
                    "A data escolhida deve corresponder a um dos dias compatíveis.",
                    nameof(requestedDate));
            }
        }
        else if (requestedDate is not null)
        {
            throw new ArgumentException(
                "Uma solicitação semanal não deve possuir uma data única.",
                nameof(requestedDate));
        }

        return normalizedDays;
    }
}
