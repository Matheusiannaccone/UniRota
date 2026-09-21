using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IPlaceService
{
    Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<SelectedPlace> GetPlaceAsync(
        string placeId,
        CancellationToken cancellationToken = default);
}
