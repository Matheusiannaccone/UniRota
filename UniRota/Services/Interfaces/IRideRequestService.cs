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

    Task<IReadOnlyList<RideRequest>> GetMyActiveRequestsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RideRequest>> GetReceivedPendingRequestsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RideRequest>> GetMyAcceptedRequestsAsync(
        CancellationToken cancellationToken = default);

    Task AcceptAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    Task RejectAsync(
        string requestId,
        CancellationToken cancellationToken = default);
}
