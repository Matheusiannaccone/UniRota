using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IMapRouteService
{
    Task<MapRouteResult> CalculateAsync(
        SelectedPlace origin,
        SelectedPlace destination,
        IReadOnlyList<SelectedPlace>? waypoints = null,
        CancellationToken cancellationToken = default);
}
