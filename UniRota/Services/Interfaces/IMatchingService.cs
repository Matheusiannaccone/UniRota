using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IMatchingService
{
    Task<IReadOnlyList<MatchResult>> FindMatchesAsync(
        WeeklyRoute passengerRoute,
        IEnumerable<WeeklyRoute> candidateRoutes,
        CancellationToken cancellationToken = default);
}
