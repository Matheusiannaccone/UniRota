using UniRota.Models;

namespace UniRota.Services;

public static class RideRequestRules
{
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
