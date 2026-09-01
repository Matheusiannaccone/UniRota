using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IRouteService
{
    Task<WeeklyRoute> CreateAsync(
        WeeklyRoute route,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WeeklyRoute>> GetMyRoutesAsync(
        CancellationToken cancellationToken = default);
}
