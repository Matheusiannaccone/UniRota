using UniRota.Models;
using UniRota.Services;
using UniRota.Services.Interfaces;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class MatchingServiceTests
{
    private const int DefaultDepartureTimeMinutes = 480;

    [Fact]
    public async Task FindMatchesAsync_ReturnsMatchWithGeographicMetrics()
    {
        var mapService = new FakeMapRouteService();
        var driver = CreateDriver(estimatedDistanceKm: 999m);

        var match = Assert.Single(await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [driver]));

        Assert.Same(driver, match.DriverRoute);
        Assert.Equal([DayOfWeek.Monday], match.CompatibleDays);
        Assert.Equal(0, match.TimeDifferenceMinutes);
        Assert.Equal(10m, match.BaseDistanceKm);
        Assert.Equal(30m, match.BaseDurationMinutes);
        Assert.Equal(12m, match.SharedDistanceKm);
        Assert.Equal(40m, match.SharedDurationMinutes);
        Assert.Equal(2m, match.DetourDistanceKm);
        Assert.Equal(10m, match.DetourDurationMinutes);
        Assert.Equal("shared-polyline", match.SharedEncodedPolyline);
        Assert.Equal(2, mapService.Requests.Count);
    }

    [Fact]
    public async Task FindMatchesAsync_WrongRoleDoesNotCallMapService()
    {
        var mapService = new FakeMapRouteService();
        var candidate = CreateDriver(role: RouteRole.Passenger);

        var matches = await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [candidate]);

        Assert.Empty(matches);
        Assert.Empty(mapService.Requests);
    }

    [Fact]
    public async Task FindMatchesAsync_SameUserDoesNotCallMapService()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(userId: "same-user");
        var driver = CreateDriver(userId: "same-user");

        var matches = await CreateService(mapService)
            .FindMatchesAsync(passenger, [driver]);

        Assert.Empty(matches);
        Assert.Empty(mapService.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task FindMatchesAsync_NoSeatsDoesNotCallMapService(int? seats)
    {
        var mapService = new FakeMapRouteService();
        var driver = CreateDriver(availableSeats: seats);

        var matches = await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [driver]);

        Assert.Empty(matches);
        Assert.Empty(mapService.Requests);
    }

    [Fact]
    public async Task FindMatchesAsync_NoCompatibleDaysDoesNotCallMapService()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(days: [DayOfWeek.Monday]);
        var driver = CreateDriver(days: [DayOfWeek.Tuesday]);

        var matches = await CreateService(mapService)
            .FindMatchesAsync(passenger, [driver]);

        Assert.Empty(matches);
        Assert.Empty(mapService.Requests);
    }

    [Fact]
    public async Task FindMatchesAsync_TimeDifferenceAboveLimitDoesNotCallMapService()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(departureTimeMinutes: 600);
        var driver = CreateDriver(departureTimeMinutes: 631);

        var matches = await CreateService(mapService)
            .FindMatchesAsync(passenger, [driver]);

        Assert.Empty(matches);
        Assert.Empty(mapService.Requests);
    }

    [Fact]
    public async Task FindMatchesAsync_DoesNotUseCircularTimeAcrossMidnight()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(departureTimeMinutes: 1430);
        var driver = CreateDriver(departureTimeMinutes: 10);

        var matches = await CreateService(mapService)
            .FindMatchesAsync(passenger, [driver]);

        Assert.Empty(matches);
        Assert.Empty(mapService.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public async Task FindMatchesAsync_AcceptsInclusiveTimeBoundary(
        int timeDifferenceMinutes)
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(departureTimeMinutes: 600);
        var driver = CreateDriver(
            departureTimeMinutes: 600 + timeDifferenceMinutes);

        var match = Assert.Single(await CreateService(mapService)
            .FindMatchesAsync(passenger, [driver]));

        Assert.Equal(timeDifferenceMinutes, match.TimeDifferenceMinutes);
    }

    [Fact]
    public async Task FindMatchesAsync_PassengerWithoutPlaceIdsDoesNotCallMapService()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(originPlaceId: string.Empty);

        var matches = await CreateService(mapService)
            .FindMatchesAsync(passenger, [CreateDriver()]);

        Assert.Empty(matches);
        Assert.Empty(mapService.Requests);
    }

    [Fact]
    public async Task FindMatchesAsync_PassengerWithEqualPlaceIdsDoesNotCallMapService()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(
            originPlaceId: "same-place",
            destinationPlaceId: "same-place");

        var matches = await CreateService(mapService)
            .FindMatchesAsync(passenger, [CreateDriver()]);

        Assert.Empty(matches);
        Assert.Empty(mapService.Requests);
    }

    [Fact]
    public async Task FindMatchesAsync_DriverWithoutPlaceIdsDoesNotCallMapService()
    {
        var mapService = new FakeMapRouteService();
        var driver = CreateDriver(destinationPlaceId: string.Empty);

        var matches = await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [driver]);

        Assert.Empty(matches);
        Assert.Empty(mapService.Requests);
    }

    [Fact]
    public async Task FindMatchesAsync_DoesNotRequireEqualAddressText()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(
            origin: "Rua do Passageiro",
            destination: "Destino do Passageiro");
        var driver = CreateDriver(
            origin: "Bairro do Motorista",
            destination: "Destino do Motorista");

        var matches = await CreateService(mapService)
            .FindMatchesAsync(passenger, [driver]);

        Assert.Single(matches);
    }

    [Fact]
    public async Task FindMatchesAsync_CalculatesDriverBaseRouteWithoutIntermediates()
    {
        var mapService = new FakeMapRouteService();
        var driver = CreateDriver(
            originPlaceId: "driver-start",
            destinationPlaceId: "driver-end");

        await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [driver]);

        var request = mapService.Requests[0];
        Assert.Equal("driver-start", request.OriginPlaceId);
        Assert.Equal("driver-end", request.DestinationPlaceId);
        Assert.Empty(request.IntermediatePlaceIds);
    }

    [Fact]
    public async Task FindMatchesAsync_SharedRouteUsesPassengerWaypointsInOrder()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(
            originPlaceId: "passenger-start",
            destinationPlaceId: "passenger-end");

        await CreateService(mapService)
            .FindMatchesAsync(passenger, [CreateDriver()]);

        var request = mapService.Requests[1];
        Assert.Equal(
            ["passenger-start", "passenger-end"],
            request.IntermediatePlaceIds);
    }

    [Fact]
    public async Task FindMatchesAsync_RemovesConsecutiveDuplicateWaypoints()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(
            originPlaceId: "shared-start",
            destinationPlaceId: "passenger-end");
        var driver = CreateDriver(
            originPlaceId: "shared-start",
            destinationPlaceId: "driver-end");

        await CreateService(mapService)
            .FindMatchesAsync(passenger, [driver]);

        Assert.Equal(2, mapService.Requests.Count);
        Assert.Equal(
            ["passenger-end"],
            mapService.Requests[1].IntermediatePlaceIds);
    }

    [Fact]
    public async Task FindMatchesAsync_ReusesBaseRouteWhenSharedRouteCollapsesToIt()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(
            originPlaceId: "shared-start",
            destinationPlaceId: "shared-end");
        var driver = CreateDriver(
            originPlaceId: "shared-start",
            destinationPlaceId: "shared-end");

        var match = Assert.Single(await CreateService(mapService)
            .FindMatchesAsync(passenger, [driver]));

        Assert.Single(mapService.Requests);
        Assert.Equal(0m, match.DetourDistanceKm);
        Assert.Equal(0m, match.DetourDurationMinutes);
    }

    [Fact]
    public async Task FindMatchesAsync_CachesEqualRouteRequestsWithinSearch()
    {
        var mapService = new FakeMapRouteService();
        var first = CreateDriver(
            id: "first",
            userId: "driver-one",
            originPlaceId: "shared-driver-start",
            destinationPlaceId: "shared-driver-end");
        var second = CreateDriver(
            id: "second",
            userId: "driver-two",
            originPlaceId: "shared-driver-start",
            destinationPlaceId: "shared-driver-end");

        var matches = await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [first, second]);

        Assert.Equal(2, matches.Count);
        Assert.Equal(2, mapService.Requests.Count);
    }

    [Fact]
    public async Task FindMatchesAsync_CalculatesDistanceAndDurationDetours()
    {
        var mapService = CreateMapService(
            baseDistanceMeters: 10000,
            baseDurationMinutes: 30,
            sharedDistanceMeters: 13500,
            sharedDurationMinutes: 42.5);

        var match = Assert.Single(await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [CreateDriver()]));

        Assert.Equal(10m, match.BaseDistanceKm);
        Assert.Equal(30m, match.BaseDurationMinutes);
        Assert.Equal(13.5m, match.SharedDistanceKm);
        Assert.Equal(42.5m, match.SharedDurationMinutes);
        Assert.Equal(3.5m, match.DetourDistanceKm);
        Assert.Equal(12.5m, match.DetourDurationMinutes);
    }

    [Fact]
    public async Task FindMatchesAsync_NormalizesNegativeDetoursToZero()
    {
        var mapService = CreateMapService(
            baseDistanceMeters: 10000,
            baseDurationMinutes: 30,
            sharedDistanceMeters: 9000,
            sharedDurationMinutes: 25);

        var match = Assert.Single(await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [CreateDriver()]));

        Assert.Equal(0m, match.DetourDistanceKm);
        Assert.Equal(0m, match.DetourDurationMinutes);
    }

    [Fact]
    public async Task FindMatchesAsync_RemovesCandidateAboveDistanceLimit()
    {
        var mapService = CreateMapService(
            baseDistanceMeters: 10000,
            baseDurationMinutes: 30,
            sharedDistanceMeters: 15001,
            sharedDurationMinutes: 40);

        var matches = await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [CreateDriver()]);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task FindMatchesAsync_RemovesCandidateAboveDurationLimit()
    {
        var mapService = CreateMapService(
            baseDistanceMeters: 10000,
            baseDurationMinutes: 30,
            sharedDistanceMeters: 14000,
            sharedDurationMinutes: 45.01);

        var matches = await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [CreateDriver()]);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task FindMatchesAsync_AcceptsInclusiveDetourLimits()
    {
        var mapService = CreateMapService(
            baseDistanceMeters: 10000,
            baseDurationMinutes: 30,
            sharedDistanceMeters: 15000,
            sharedDurationMinutes: 45);

        var match = Assert.Single(await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [CreateDriver()]));

        Assert.Equal(5m, match.DetourDistanceKm);
        Assert.Equal(15m, match.DetourDurationMinutes);
    }

    [Fact]
    public async Task FindMatchesAsync_CandidateFailureDoesNotStopOtherCandidates()
    {
        var mapService = new FakeMapRouteService
        {
            Handler = (request, cancellationToken) =>
            {
                if (request.OriginPlaceId == "bad-origin")
                {
                    throw new InvalidOperationException("Route failed.");
                }

                return Task.FromResult(request.IntermediatePlaceIds.Count == 0
                    ? CreateMapRoute(10000, 30)
                    : CreateMapRoute(12000, 40));
            }
        };
        var badDriver = CreateDriver(
            id: "bad",
            originPlaceId: "bad-origin");
        var goodDriver = CreateDriver(
            id: "good",
            originPlaceId: "good-origin");

        var match = Assert.Single(await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), [badDriver, goodDriver]));

        Assert.Equal("good", match.DriverRoute.Id);
    }

    [Fact]
    public async Task FindMatchesAsync_GlobalAuthenticationFailureIsPropagated()
    {
        var exception = new InvalidOperationException("Sessão expirada.");
        exception.Data["FunctionStatus"] = "UNAUTHENTICATED";
        var mapService = new FakeMapRouteService
        {
            Handler = (request, cancellationToken) => throw exception
        };

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(mapService).FindMatchesAsync(
                CreatePassenger(),
                [CreateDriver()]));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task FindMatchesAsync_RespectsCancellation()
    {
        var requestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var mapService = new FakeMapRouteService
        {
            Handler = async (request, cancellationToken) =>
            {
                requestStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return CreateMapRoute(10000, 30);
            }
        };
        using var cancellation = new CancellationTokenSource();

        var matchingTask = CreateService(mapService).FindMatchesAsync(
            CreatePassenger(),
            [CreateDriver()],
            cancellation.Token);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => matchingTask);
    }

    [Fact]
    public async Task FindMatchesAsync_OrdersByDetourDurationFirst()
    {
        var mapService = CreateOrderingMapService(new Dictionary<string, (long, double)>
        {
            ["driver-origin-slower"] = (11000, 40),
            ["driver-origin-faster"] = (14000, 35)
        });

        var matches = await CreateService(mapService).FindMatchesAsync(
            CreatePassenger(),
            [CreateDriver(id: "slower"), CreateDriver(id: "faster")]);

        Assert.Equal(
            ["faster", "slower"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public async Task FindMatchesAsync_UsesDetourDistanceAsSecondTieBreaker()
    {
        var mapService = CreateOrderingMapService(new Dictionary<string, (long, double)>
        {
            ["driver-origin-farther"] = (14000, 35),
            ["driver-origin-nearer"] = (11000, 35)
        });

        var matches = await CreateService(mapService).FindMatchesAsync(
            CreatePassenger(),
            [CreateDriver(id: "farther"), CreateDriver(id: "nearer")]);

        Assert.Equal(
            ["nearer", "farther"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public async Task FindMatchesAsync_UsesTimeDifferenceAsThirdTieBreaker()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(departureTimeMinutes: 600);
        var farther = CreateDriver(id: "farther", departureTimeMinutes: 620);
        var closer = CreateDriver(id: "closer", departureTimeMinutes: 605);

        var matches = await CreateService(mapService).FindMatchesAsync(
            passenger,
            [farther, closer]);

        Assert.Equal(
            ["closer", "farther"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public async Task FindMatchesAsync_UsesCompatibleDaysAsFourthTieBreaker()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(
            days: [DayOfWeek.Monday, DayOfWeek.Friday]);
        var fewerDays = CreateDriver(
            id: "fewer",
            days: [DayOfWeek.Monday]);
        var moreDays = CreateDriver(
            id: "more",
            days: [DayOfWeek.Monday, DayOfWeek.Friday]);

        var matches = await CreateService(mapService).FindMatchesAsync(
            passenger,
            [fewerDays, moreDays]);

        Assert.Equal(
            ["more", "fewer"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public async Task FindMatchesAsync_UsesDepartureTimeAfterEqualDifference()
    {
        var mapService = new FakeMapRouteService();
        var passenger = CreatePassenger(departureTimeMinutes: 600);
        var later = CreateDriver(id: "later", departureTimeMinutes: 610);
        var earlier = CreateDriver(id: "earlier", departureTimeMinutes: 590);

        var matches = await CreateService(mapService).FindMatchesAsync(
            passenger,
            [later, earlier]);

        Assert.Equal(
            ["earlier", "later"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public async Task FindMatchesAsync_UsesOrdinalRouteIdAsStableFinalTieBreaker()
    {
        var mapService = new FakeMapRouteService();
        var lowercase = CreateDriver(id: "a");
        var uppercase = CreateDriver(id: "A");
        var empty = CreateDriver(id: string.Empty);

        var matches = await CreateService(mapService).FindMatchesAsync(
            CreatePassenger(),
            [lowercase, empty, uppercase]);

        Assert.Equal(
            [string.Empty, "A", "a"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public async Task FindMatchesAsync_ReturnsDistinctOrderedCompatibleDays()
    {
        var passenger = CreatePassenger(
            days:
            [
                DayOfWeek.Monday,
                DayOfWeek.Wednesday,
                DayOfWeek.Friday,
                DayOfWeek.Monday
            ]);
        var driver = CreateDriver(
            days:
            [
                DayOfWeek.Friday,
                DayOfWeek.Thursday,
                DayOfWeek.Monday,
                DayOfWeek.Friday
            ]);

        var match = Assert.Single(await CreateService(new FakeMapRouteService())
            .FindMatchesAsync(passenger, [driver]));

        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Friday],
            match.CompatibleDays);
    }

    [Fact]
    public async Task FindMatchesAsync_IgnoresMalformedCandidatesAndKeepsValidOnes()
    {
        var mapService = new FakeMapRouteService();
        var malformedCandidates = new WeeklyRoute[]
        {
            null!,
            CreateDriver(id: "wrong-role", role: RouteRole.Passenger),
            CreateDriver(id: "empty-user", userId: " "),
            CreateDriver(id: "empty-origin", origin: " "),
            CreateDriver(id: "empty-destination", destination: " "),
            CreateDriver(id: "empty-origin-place", originPlaceId: " "),
            CreateDriver(id: "empty-destination-place", destinationPlaceId: " "),
            CreateDriver(id: "no-days", days: []),
            CreateDriver(id: "invalid-day", days: [(DayOfWeek)99]),
            CreateDriver(id: "invalid-time", departureTimeMinutes: 1440),
            CreateDriver(id: "no-seats", availableSeats: 0),
            CreateDriver(id: "valid")
        };

        var match = Assert.Single(await CreateService(mapService)
            .FindMatchesAsync(CreatePassenger(), malformedCandidates));

        Assert.Equal("valid", match.DriverRoute.Id);
        Assert.Equal(2, mapService.Requests.Count);
    }

    [Fact]
    public async Task FindMatchesAsync_ThrowsForInvalidReferenceRoute()
    {
        var service = CreateService(new FakeMapRouteService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.FindMatchesAsync(
                CreatePassenger(role: RouteRole.Driver),
                []));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.FindMatchesAsync(
                CreatePassenger(departureTimeMinutes: 1440),
                []));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.FindMatchesAsync(
                CreatePassenger(days: []),
                []));
    }

    [Fact]
    public async Task FindMatchesAsync_ThrowsForNullArguments()
    {
        var service = CreateService(new FakeMapRouteService());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.FindMatchesAsync(null!, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.FindMatchesAsync(CreatePassenger(), null!));
    }

    [Fact]
    public void MatchingOptions_CentralizesConservativeDefaults()
    {
        var options = new MatchingOptions();

        Assert.Equal(30, options.MaximumDepartureTimeDifferenceMinutes);
        Assert.Equal(5m, options.MaximumDetourDistanceKm);
        Assert.Equal(15m, options.MaximumDetourDurationMinutes);
    }

    [Theory]
    [InlineData(6.0, 6)]
    [InlineData(6.1, 7)]
    [InlineData(6.9, 7)]
    public void MatchResultItemViewModel_RoundsDisplayedMinutesUp(
        double durationMinutes,
        int expectedDisplayedMinutes)
    {
        var realDurationMinutes = Convert.ToDecimal(durationMinutes);
        var match = new MatchResult(
            CreateDriver(),
            [DayOfWeek.Monday],
            0,
            DetourDistanceKm: 2.5m,
            DetourDurationMinutes: realDurationMinutes);

        var item = new MatchResultItemViewModel(match);

        Assert.Equal(
            $"Desvio: +2,5 km · +{expectedDisplayedMinutes} min",
            item.DetourText);
        Assert.Equal(realDurationMinutes, match.DetourDurationMinutes);
    }

    private static MatchingService CreateService(
        IMapRouteService mapRouteService,
        MatchingOptions? options = null)
    {
        return new MatchingService(
            mapRouteService,
            options ?? new MatchingOptions());
    }

    private static FakeMapRouteService CreateMapService(
        long baseDistanceMeters,
        double baseDurationMinutes,
        long sharedDistanceMeters,
        double sharedDurationMinutes)
    {
        return new FakeMapRouteService
        {
            Handler = (request, cancellationToken) => Task.FromResult(
                request.IntermediatePlaceIds.Count == 0
                    ? CreateMapRoute(baseDistanceMeters, baseDurationMinutes)
                    : CreateMapRoute(sharedDistanceMeters, sharedDurationMinutes))
        };
    }

    private static FakeMapRouteService CreateOrderingMapService(
        IReadOnlyDictionary<string, (long DistanceMeters, double DurationMinutes)>
            sharedRoutes)
    {
        return new FakeMapRouteService
        {
            Handler = (request, cancellationToken) =>
            {
                if (request.IntermediatePlaceIds.Count == 0)
                {
                    return Task.FromResult(CreateMapRoute(10000, 30));
                }

                var route = sharedRoutes[request.OriginPlaceId];
                return Task.FromResult(CreateMapRoute(
                    route.DistanceMeters,
                    route.DurationMinutes));
            }
        };
    }

    private static MapRouteResult CreateMapRoute(
        long distanceMeters,
        double durationMinutes,
        string encodedPolyline = "")
    {
        return new MapRouteResult
        {
            DistanceMeters = distanceMeters,
            Duration = TimeSpan.FromMinutes(durationMinutes),
            EncodedPolyline = encodedPolyline
        };
    }

    private static WeeklyRoute CreatePassenger(
        string id = "passenger-route",
        string userId = "passenger-user",
        RouteRole role = RouteRole.Passenger,
        string origin = "Origem do passageiro",
        string originPlaceId = "passenger-origin",
        string destination = "Destino do passageiro",
        string destinationPlaceId = "passenger-destination",
        IReadOnlyList<DayOfWeek>? days = null,
        int departureTimeMinutes = DefaultDepartureTimeMinutes)
    {
        return new WeeklyRoute
        {
            Id = id,
            UserId = userId,
            UserName = "Passageiro",
            Role = role,
            Origin = origin,
            OriginPlaceId = originPlaceId,
            Destination = destination,
            DestinationPlaceId = destinationPlaceId,
            DaysOfWeek = days ?? [DayOfWeek.Monday],
            DepartureTimeMinutes = departureTimeMinutes,
            AvailableSeats = null,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
    }

    private static WeeklyRoute CreateDriver(
        string id = "driver-route",
        string userId = "driver-user",
        RouteRole role = RouteRole.Driver,
        string origin = "Origem do motorista",
        string? originPlaceId = null,
        string destination = "Destino do motorista",
        string? destinationPlaceId = null,
        IReadOnlyList<DayOfWeek>? days = null,
        int departureTimeMinutes = DefaultDepartureTimeMinutes,
        int? availableSeats = 1,
        decimal estimatedDistanceKm = 10m)
    {
        return new WeeklyRoute
        {
            Id = id,
            UserId = userId,
            UserName = "Motorista",
            Role = role,
            Origin = origin,
            OriginPlaceId = originPlaceId ?? $"driver-origin-{id}",
            Destination = destination,
            DestinationPlaceId = destinationPlaceId ?? $"driver-destination-{id}",
            DaysOfWeek = days ?? [DayOfWeek.Monday],
            DepartureTimeMinutes = departureTimeMinutes,
            AvailableSeats = availableSeats,
            EstimatedDistanceKm = estimatedDistanceKm,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
    }

    private sealed record RouteRequest(
        string OriginPlaceId,
        string DestinationPlaceId,
        IReadOnlyList<string> IntermediatePlaceIds);

    private sealed class FakeMapRouteService : IMapRouteService
    {
        public List<RouteRequest> Requests { get; } = [];

        public Func<
            RouteRequest,
            CancellationToken,
            Task<MapRouteResult>>? Handler { get; init; }

        public Task<MapRouteResult> CalculateAsync(
            string originPlaceId,
            string destinationPlaceId,
            IReadOnlyList<string>? intermediatePlaceIds = null,
            CancellationToken cancellationToken = default)
        {
            var request = new RouteRequest(
                originPlaceId,
                destinationPlaceId,
                intermediatePlaceIds?.ToArray() ?? []);
            Requests.Add(request);

            if (Handler is not null)
            {
                return Handler(request, cancellationToken);
            }

            return Task.FromResult(request.IntermediatePlaceIds.Count == 0
                ? CreateMapRoute(10000, 30, "base-polyline")
                : CreateMapRoute(12000, 40, "shared-polyline"));
        }
    }
}
