using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IMapRouteService
{
    Task<MapRouteResult> CalculateAsync(
        string originPlaceId,
        string destinationPlaceId,
        CancellationToken cancellationToken = default);
}
