using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IPlaceService
{
    PlaceAutocompleteSession CreateSession();

    Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(
        string query,
        PlaceAutocompleteSession session,
        CancellationToken cancellationToken = default);

    Task<SelectedPlace> GetPlaceAsync(
        string placeId,
        PlaceAutocompleteSession session,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, MapCoordinate>> GetCoordinatesAsync(
        IReadOnlyList<string> placeIds,
        CancellationToken cancellationToken = default);
}
