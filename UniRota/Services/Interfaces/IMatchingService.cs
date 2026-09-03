using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IMatchingService
{
    IReadOnlyList<MatchResult> FindMatches(
        WeeklyRoute passengerRoute,
        IEnumerable<WeeklyRoute> candidateRoutes);
}
