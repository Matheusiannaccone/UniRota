using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IRideRequestService
{
    Task<RideRequest> CreateAsync(
        string passengerRouteId,
        MatchResult match,
        RideRequestType type,
        DateOnly? requestedDate,
        CancellationToken cancellationToken = default);

    Task<bool> HasPendingRequestAsync(
        string passengerRouteId,
        string driverRouteId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RideRequest>> GetMyPendingRequestsAsync(
        CancellationToken cancellationToken = default);
}
