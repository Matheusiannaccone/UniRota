using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.Services;

public sealed class MatchingService : IMatchingService
{
    private static readonly HashSet<string> GlobalFailureStatuses = new(
        StringComparer.Ordinal)
    {
        "CONFIGURATION_ERROR",
        "PERMISSION_DENIED",
        "RESOURCE_EXHAUSTED",
        "UNAUTHENTICATED"
    };

    private readonly IMapRouteService _mapRouteService;
    private readonly MatchingOptions _options;

    public MatchingService(
        IMapRouteService mapRouteService,
        MatchingOptions options)
    {
        _mapRouteService = mapRouteService;
        _options = options;
        ValidateOptions(options);
    }

    public async Task<IReadOnlyList<MatchResult>> FindMatchesAsync(
        WeeklyRoute passengerRoute,
        IEnumerable<WeeklyRoute> candidateRoutes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passengerRoute);
        ArgumentNullException.ThrowIfNull(candidateRoutes);

        ValidatePassengerRoute(passengerRoute);

        if (!HasValidPlaceIds(passengerRoute))
        {
            return [];
        }

        var passengerOriginPlaceId = NormalizePlaceId(
            passengerRoute.OriginPlaceId);
        var passengerDestinationPlaceId = NormalizePlaceId(
            passengerRoute.DestinationPlaceId);

        if (string.Equals(
                passengerOriginPlaceId,
                passengerDestinationPlaceId,
                StringComparison.Ordinal))
        {
            return [];
        }

        var passengerDays = passengerRoute.DaysOfWeek.ToHashSet();
        var candidates = new List<CandidateContext>();

        foreach (var driverRoute in candidateRoutes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsValidDriverCandidate(driverRoute)
                || string.Equals(
                    passengerRoute.UserId,
                    driverRoute.UserId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var compatibleDays = driverRoute.DaysOfWeek
                .Where(passengerDays.Contains)
                .Distinct()
                .OrderBy(day => day)
                .ToArray();

            if (compatibleDays.Length == 0)
            {
                continue;
            }

            var timeDifferenceMinutes = Math.Abs(
                passengerRoute.DepartureTimeMinutes
                - driverRoute.DepartureTimeMinutes);

            if (timeDifferenceMinutes
                > _options.MaximumDepartureTimeDifferenceMinutes)
            {
                continue;
            }

            candidates.Add(new CandidateContext(
                driverRoute,
                compatibleDays,
                timeDifferenceMinutes));
        }

        var routeCache = new Dictionary<RouteRequestKey, MapRouteResult>();
        var failedRoutes = new HashSet<RouteRequestKey>();
        var matches = new List<MatchResult>();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var driverOriginPlaceId = NormalizePlaceId(
                candidate.DriverRoute.OriginPlaceId);
            var driverDestinationPlaceId = NormalizePlaceId(
                candidate.DriverRoute.DestinationPlaceId);

            var baseRoute = await TryGetRouteAsync(
                driverOriginPlaceId,
                driverDestinationPlaceId,
                [],
                routeCache,
                failedRoutes,
                cancellationToken);

            if (baseRoute is null)
            {
                continue;
            }

            var sharedIntermediatePlaceIds = BuildSharedIntermediates(
                driverOriginPlaceId,
                passengerOriginPlaceId,
                passengerDestinationPlaceId,
                driverDestinationPlaceId);
            var sharedRoute = await TryGetRouteAsync(
                driverOriginPlaceId,
                driverDestinationPlaceId,
                sharedIntermediatePlaceIds,
                routeCache,
                failedRoutes,
                cancellationToken);

            if (sharedRoute is null)
            {
                continue;
            }

            var baseDistanceKm = ToKilometers(baseRoute.DistanceMeters);
            var baseDurationMinutes = ToMinutes(baseRoute.Duration);
            var sharedDistanceKm = ToKilometers(sharedRoute.DistanceMeters);
            var sharedDurationMinutes = ToMinutes(sharedRoute.Duration);
            var detourDistanceKm = Math.Max(
                0m,
                sharedDistanceKm - baseDistanceKm);
            var detourDurationMinutes = Math.Max(
                0m,
                sharedDurationMinutes - baseDurationMinutes);

            if (detourDistanceKm > _options.MaximumDetourDistanceKm
                || detourDurationMinutes
                    > _options.MaximumDetourDurationMinutes)
            {
                continue;
            }

            matches.Add(new MatchResult(
                candidate.DriverRoute,
                candidate.CompatibleDays,
                candidate.TimeDifferenceMinutes,
                baseDistanceKm,
                baseDurationMinutes,
                sharedDistanceKm,
                sharedDurationMinutes,
                detourDistanceKm,
                detourDurationMinutes,
                sharedRoute.EncodedPolyline));
        }

        return matches
            .OrderBy(result => result.DetourDurationMinutes)
            .ThenBy(result => result.DetourDistanceKm)
            .ThenBy(result => result.TimeDifferenceMinutes)
            .ThenByDescending(result => result.CompatibleDays.Count)
            .ThenBy(result => result.DriverRoute.DepartureTimeMinutes)
            .ThenBy(
                result => result.DriverRoute.Id ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<MapRouteResult?> TryGetRouteAsync(
        string originPlaceId,
        string destinationPlaceId,
        IReadOnlyList<string> intermediatePlaceIds,
        IDictionary<RouteRequestKey, MapRouteResult> routeCache,
        ISet<RouteRequestKey> failedRoutes,
        CancellationToken cancellationToken)
    {
        var key = RouteRequestKey.Create(
            originPlaceId,
            destinationPlaceId,
            intermediatePlaceIds);

        if (routeCache.TryGetValue(key, out var cachedRoute))
        {
            return cachedRoute;
        }

        if (failedRoutes.Contains(key))
        {
            return null;
        }

        try
        {
            var route = await _mapRouteService.CalculateAsync(
                originPlaceId,
                destinationPlaceId,
                intermediatePlaceIds,
                cancellationToken);

            if (route.DistanceMeters <= 0 || route.Duration <= TimeSpan.Zero)
            {
                failedRoutes.Add(key);
                return null;
            }

            routeCache[key] = route;
            return route;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (!IsGlobalFailure(exception))
        {
            failedRoutes.Add(key);
            return null;
        }
    }

    private static IReadOnlyList<string> BuildSharedIntermediates(
        string driverOriginPlaceId,
        string passengerOriginPlaceId,
        string passengerDestinationPlaceId,
        string driverDestinationPlaceId)
    {
        var routeSequence = new List<string>(4);

        foreach (var placeId in new[]
                 {
                     driverOriginPlaceId,
                     passengerOriginPlaceId,
                     passengerDestinationPlaceId,
                     driverDestinationPlaceId
                 })
        {
            if (routeSequence.Count == 0
                || !string.Equals(
                    routeSequence[^1],
                    placeId,
                    StringComparison.Ordinal))
            {
                routeSequence.Add(placeId);
            }
        }

        return routeSequence
            .Skip(1)
            .Take(Math.Max(0, routeSequence.Count - 2))
            .ToArray();
    }

    private static void ValidateOptions(MatchingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumDepartureTimeDifferenceMinutes < 0
            || options.MaximumDetourDistanceKm < 0m
            || options.MaximumDetourDurationMinutes < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Os limites do matching não podem ser negativos.");
        }
    }

    private static void ValidatePassengerRoute(WeeklyRoute passengerRoute)
    {
        if (passengerRoute.Role != RouteRole.Passenger)
        {
            throw new ArgumentException(
                "A rota de referência deve possuir o papel de passageiro.",
                nameof(passengerRoute));
        }

        if (string.IsNullOrWhiteSpace(passengerRoute.UserId))
        {
            throw new ArgumentException(
                "A rota de referência deve possuir um identificador de usuário.",
                nameof(passengerRoute));
        }

        if (string.IsNullOrWhiteSpace(passengerRoute.Origin))
        {
            throw new ArgumentException(
                "A rota de referência deve possuir uma origem.",
                nameof(passengerRoute));
        }

        if (string.IsNullOrWhiteSpace(passengerRoute.Destination))
        {
            throw new ArgumentException(
                "A rota de referência deve possuir um destino.",
                nameof(passengerRoute));
        }

        if (passengerRoute.DaysOfWeek is null
            || passengerRoute.DaysOfWeek.Count == 0
            || passengerRoute.DaysOfWeek.Any(
                day => !Enum.IsDefined(typeof(DayOfWeek), day)))
        {
            throw new ArgumentException(
                "A rota de referência deve possuir ao menos um dia válido.",
                nameof(passengerRoute));
        }

        if (passengerRoute.DepartureTimeMinutes is < 0 or > 1439)
        {
            throw new ArgumentOutOfRangeException(
                nameof(passengerRoute),
                passengerRoute.DepartureTimeMinutes,
                "O horário da rota de referência deve estar entre 0 e 1439 minutos.");
        }
    }

    private static bool IsValidDriverCandidate(WeeklyRoute? route)
    {
        return route is not null
            && route.Role == RouteRole.Driver
            && !string.IsNullOrWhiteSpace(route.UserId)
            && !string.IsNullOrWhiteSpace(route.Origin)
            && !string.IsNullOrWhiteSpace(route.Destination)
            && HasValidPlaceIds(route)
            && !string.Equals(
                NormalizePlaceId(route.OriginPlaceId),
                NormalizePlaceId(route.DestinationPlaceId),
                StringComparison.Ordinal)
            && route.DaysOfWeek is not null
            && route.DaysOfWeek.Count > 0
            && route.DaysOfWeek.All(
                day => Enum.IsDefined(typeof(DayOfWeek), day))
            && route.DepartureTimeMinutes is >= 0 and <= 1439
            && route.AvailableSeats is > 0;
    }

    private static bool HasValidPlaceIds(WeeklyRoute route)
    {
        return !string.IsNullOrWhiteSpace(route.OriginPlaceId)
            && !string.IsNullOrWhiteSpace(route.DestinationPlaceId);
    }

    private static bool IsGlobalFailure(Exception exception)
    {
        return exception.Data["FunctionStatus"] is string status
            && GlobalFailureStatuses.Contains(status);
    }

    private static string NormalizePlaceId(string value) => value.Trim();

    private static decimal ToKilometers(long distanceMeters) =>
        distanceMeters / 1000m;

    private static decimal ToMinutes(TimeSpan duration) =>
        duration.Ticks / (decimal)TimeSpan.TicksPerMinute;

    private sealed record CandidateContext(
        WeeklyRoute DriverRoute,
        IReadOnlyList<DayOfWeek> CompatibleDays,
        int TimeDifferenceMinutes);

    private readonly record struct RouteRequestKey(
        string OriginPlaceId,
        string DestinationPlaceId,
        string FirstIntermediatePlaceId,
        string SecondIntermediatePlaceId)
    {
        public static RouteRequestKey Create(
            string originPlaceId,
            string destinationPlaceId,
            IReadOnlyList<string> intermediatePlaceIds)
        {
            return new RouteRequestKey(
                originPlaceId,
                destinationPlaceId,
                intermediatePlaceIds.Count > 0
                    ? intermediatePlaceIds[0]
                    : string.Empty,
                intermediatePlaceIds.Count > 1
                    ? intermediatePlaceIds[1]
                    : string.Empty);
        }
    }
}
