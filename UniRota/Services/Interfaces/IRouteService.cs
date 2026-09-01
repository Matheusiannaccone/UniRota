using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IRouteService
{
    Task<WeeklyRoute> CreateAsync(
        WeeklyRoute route,
        CancellationToken cancellationToken = default);

    Task<WeeklyRoute> UpdateAsync(
        WeeklyRoute route,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string routeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WeeklyRoute>> GetMyRoutesAsync(
        CancellationToken cancellationToken = default);
}
