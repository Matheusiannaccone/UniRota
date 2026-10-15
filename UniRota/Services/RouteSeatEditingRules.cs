using UniRota.Models;

namespace UniRota.Services;

public static class RouteSeatEditingRules
{
    public static IReadOnlyDictionary<DayOfWeek, int> GetUpdatedWeeklySeats(
        WeeklyRoute existingRoute,
        int? newSeatCapacity,
        IReadOnlyList<DayOfWeek> selectedDays)
    {
        ArgumentNullException.ThrowIfNull(existingRoute);

        if (existingRoute.Role == RouteRole.Passenger)
        {
            if (newSeatCapacity is not null)
            {
                throw new ArgumentException("Uma rota de passageiro não possui capacidade de vagas.");
            }

            return new Dictionary<DayOfWeek, int>();
        }

        if (newSeatCapacity is null or <= 0 || existingRoute.SeatCapacity is null or <= 0)
        {
            throw new ArgumentException("Informe ao menos uma vaga para a rota de motorista.");
        }

        var oldCapacity = existingRoute.SeatCapacity.Value;
        var newCapacity = newSeatCapacity.Value;
        var newDays = selectedDays.ToHashSet();

        foreach (var removedDay in existingRoute.DaysOfWeek.Where(day => !newDays.Contains(day)))
        {
            if (existingRoute.WeeklyAvailableSeats[removedDay] < oldCapacity
                || existingRoute.OneTimeReservations.Any(reservation => reservation.DayOfWeek == removedDay))
            {
                throw new ArgumentException("Não é possível remover um dia com passageiros confirmados.");
            }
        }

        var updatedSeats = new Dictionary<DayOfWeek, int>();

        foreach (var day in selectedDays.Distinct())
        {
            var weeklyOccupied = existingRoute.WeeklyAvailableSeats.TryGetValue(day, out var oldAvailable)
                ? oldCapacity - oldAvailable
                : 0;
            var available = newCapacity - weeklyOccupied;

            if (available < 0)
            {
                throw new ArgumentException("A capacidade informada é menor que a ocupação semanal confirmada.");
            }

            updatedSeats.Add(day, available);
        }

        // P4: todas as datas armazenadas, inclusive históricas, protegem a edição.
        // Esta validação não estabelece a política temporal de matching/aceite (P3).
        foreach (var dateReservations in existingRoute.OneTimeReservations.GroupBy(reservation => reservation.Date))
        {
            if (dateReservations.Count() > updatedSeats[dateReservations.Key.DayOfWeek])
            {
                throw new ArgumentException("A capacidade informada é menor que a ocupação confirmada em uma data reservada.");
            }
        }

        return updatedSeats;
    }
}
