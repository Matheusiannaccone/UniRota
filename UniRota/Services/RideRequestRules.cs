using UniRota.Models;

namespace UniRota.Services;

public static class RideRequestRules
{
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
        days.Count > 0 && days.All(day => weeklySeats.TryGetValue(day, out var seats) && seats > 0);

    public static bool HasSeatsOnDate(
        IReadOnlyDictionary<DayOfWeek, int> weeklySeats,
        IReadOnlyList<OneTimeReservation> reservations,
        DateOnly date) =>
        weeklySeats.TryGetValue(date.DayOfWeek, out var seats)
        && seats - reservations.Count(reservation => reservation.Date == date) > 0;

    public static IReadOnlyDictionary<DayOfWeek, int> ApplyWeeklyAcceptance(
        IReadOnlyDictionary<DayOfWeek, int> weeklySeats,
        IReadOnlyList<OneTimeReservation> reservations,
        IReadOnlyList<DayOfWeek> days,
        DateOnly today)
    {
        if (!HasWeeklySeats(weeklySeats, days))
        {
            throw new InvalidOperationException("Não há vagas disponíveis em todos os dias solicitados.");
        }

        var updated = weeklySeats.ToDictionary(entry => entry.Key, entry => entry.Value);
        foreach (var day in days.Distinct())
        {
            updated[day]--;
        }

        if (reservations.Where(reservation => reservation.Date >= today
                && days.Contains(reservation.DayOfWeek))
            .GroupBy(reservation => reservation.Date)
            .Any(group => updated[group.Key.DayOfWeek] - group.Count() < 0))
        {
            throw new InvalidOperationException("Não há vagas sem comprometer reservas pontuais confirmadas.");
        }

        return updated;
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
