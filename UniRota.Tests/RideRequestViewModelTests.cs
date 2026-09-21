using UniRota.Models;
using UniRota.Services;
using UniRota.Services.Interfaces;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class RideRequestViewModelTests
{
    [Fact]
    public void SetRequestContext_CalculatesPriceFromSharedRouteDistance()
    {
        var requestService = new FakeRideRequestService();
        var pricingService = new CountingPricingService();
        var viewModel = new RideRequestViewModel(
            requestService,
            pricingService);
        var (passengerRoute, match) = CreateContext(
            sharedDistanceKm: 12.75m,
            driverEstimatedDistanceKm: 99m);

        viewModel.SetRequestContext(passengerRoute, match);

        Assert.Equal(1, pricingService.CalculateCount);
        Assert.Equal(12.75m, pricingService.LastDistanceKm);
        Assert.NotEqual(
            match.DriverRoute.EstimatedDistanceKm,
            pricingService.LastDistanceKm);
        Assert.True(viewModel.HasSuggestedPrice);
        Assert.Equal(
            "Preço sugerido: R$ 3,38 por viagem",
            viewModel.SuggestedPriceText);
    }

    [Fact]
    public async Task Submit_PassesDisplayedPriceWithoutCalculatingAgain()
    {
        var requestService = new FakeRideRequestService();
        var pricingService = new CountingPricingService();
        var viewModel = new RideRequestViewModel(
            requestService,
            pricingService);
        var (passengerRoute, match) = CreateContext(12.75m);
        viewModel.SetRequestContext(passengerRoute, match);
        viewModel.SelectedRequestType = GetRequestType(
            viewModel,
            RideRequestType.Weekly);

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(1, pricingService.CalculateCount);
        Assert.Equal(1, requestService.CreateCount);
        Assert.Equal(3.38m, requestService.LastSuggestedPrice);
        Assert.Contains("R$ 3,38", viewModel.SuggestedPriceText);
        Assert.True(viewModel.HasSubmittedSuccessfully);
    }

    [Fact]
    public async Task InvalidSharedRouteDistance_PreventsRequestCreation()
    {
        var requestService = new FakeRideRequestService();
        var pricingService = new CountingPricingService();
        var viewModel = new RideRequestViewModel(
            requestService,
            pricingService);
        var (passengerRoute, match) = CreateContext(
            sharedDistanceKm: 0m,
            driverEstimatedDistanceKm: 12.75m);

        viewModel.SetRequestContext(passengerRoute, match);

        Assert.False(viewModel.HasSuggestedPrice);
        Assert.True(viewModel.HasError);
        Assert.Contains("rota compartilhada", viewModel.ErrorMessage);

        viewModel.SelectedRequestType = GetRequestType(
            viewModel,
            RideRequestType.Weekly);
        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(1, pricingService.CalculateCount);
        Assert.Equal(0, requestService.CreateCount);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task Submit_DoesNotCallMapServiceAfterMatching()
    {
        var mapRouteService = new CountingMapRouteService();
        var passengerRoute = CreatePassengerRouteWithPlaceIds();
        var driverRoute = CreateDriverRouteWithPlaceIds();
        var match = Assert.Single(await new MatchingService(
                mapRouteService,
                new MatchingOptions())
            .FindMatchesAsync(passengerRoute, [driverRoute]));
        Assert.Equal(2, mapRouteService.CalculateCount);

        var requestService = new FakeRideRequestService();
        var viewModel = new RideRequestViewModel(
            requestService,
            new CountingPricingService());
        viewModel.SetRequestContext(passengerRoute, match);
        viewModel.SelectedRequestType = GetRequestType(
            viewModel,
            RideRequestType.Weekly);

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(2, mapRouteService.CalculateCount);
        Assert.Equal(1, requestService.CreateCount);
        Assert.Equal(3.18m, requestService.LastSuggestedPrice);
    }

    [Fact]
    public async Task Submit_PreservesOnceRequestBehaviorAndPrice()
    {
        var requestService = new FakeRideRequestService();
        var viewModel = new RideRequestViewModel(
            requestService,
            new CountingPricingService());
        var (passengerRoute, match) = CreateContext(10m);
        viewModel.SetRequestContext(passengerRoute, match);
        viewModel.SelectedRequestType = GetRequestType(
            viewModel,
            RideRequestType.Once);
        viewModel.RequestedDate = DateTime.Today;

        Assert.True(viewModel.TryGetConfirmationMessage(out _));
        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(RideRequestType.Once, requestService.LastType);
        Assert.Equal(
            DateOnly.FromDateTime(DateTime.Today),
            requestService.LastRequestedDate);
        Assert.Equal(2.65m, requestService.LastSuggestedPrice);
    }

    [Fact]
    public async Task Submit_PreservesWeeklyRequestBehaviorAndPrice()
    {
        var requestService = new FakeRideRequestService();
        var viewModel = new RideRequestViewModel(
            requestService,
            new CountingPricingService());
        var (passengerRoute, match) = CreateContext(10m);
        viewModel.SetRequestContext(passengerRoute, match);
        viewModel.SelectedRequestType = GetRequestType(
            viewModel,
            RideRequestType.Weekly);

        Assert.True(viewModel.TryGetConfirmationMessage(out _));
        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(RideRequestType.Weekly, requestService.LastType);
        Assert.Null(requestService.LastRequestedDate);
        Assert.Equal(2.65m, requestService.LastSuggestedPrice);
    }

    private static RideRequestTypeOption GetRequestType(
        RideRequestViewModel viewModel,
        RideRequestType type)
    {
        return viewModel.RequestTypes.Single(option => option.Type == type);
    }

    private static (WeeklyRoute PassengerRoute, MatchResult Match) CreateContext(
        decimal sharedDistanceKm,
        decimal driverEstimatedDistanceKm = 10m)
    {
        var currentDay = DateTime.Today.DayOfWeek;
        var passengerRoute = new WeeklyRoute
        {
            Id = "passenger-route",
            UserId = "passenger-user",
            Role = RouteRole.Passenger,
            Origin = "Centro",
            Destination = "Facens",
            DaysOfWeek = [currentDay],
            DepartureTimeMinutes = 480
        };
        var driverRoute = new WeeklyRoute
        {
            Id = "driver-route",
            UserId = "driver-user",
            UserName = "Motorista",
            Role = RouteRole.Driver,
            Origin = "Centro",
            Destination = "Facens",
            DaysOfWeek = [currentDay],
            DepartureTimeMinutes = 480,
            AvailableSeats = 1,
            EstimatedDistanceKm = driverEstimatedDistanceKm
        };

        return (
            passengerRoute,
            new MatchResult(
                driverRoute,
                [currentDay],
                0,
                SharedDistanceKm: sharedDistanceKm));
    }

    private static WeeklyRoute CreatePassengerRouteWithPlaceIds()
    {
        return new WeeklyRoute
        {
            Id = "passenger-route",
            UserId = "passenger-user",
            UserName = "Passageiro",
            Role = RouteRole.Passenger,
            Origin = "Origem do passageiro",
            OriginPlaceId = "passenger-origin",
            Destination = "Destino do passageiro",
            DestinationPlaceId = "passenger-destination",
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 480
        };
    }

    private static WeeklyRoute CreateDriverRouteWithPlaceIds()
    {
        return new WeeklyRoute
        {
            Id = "driver-route",
            UserId = "driver-user",
            UserName = "Motorista",
            Role = RouteRole.Driver,
            Origin = "Origem do motorista",
            OriginPlaceId = "driver-origin",
            Destination = "Destino do motorista",
            DestinationPlaceId = "driver-destination",
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 480,
            AvailableSeats = 1,
            EstimatedDistanceKm = 10m
        };
    }

    private sealed class CountingPricingService : IPricingService
    {
        private readonly PricingService _innerService = new();

        public int CalculateCount { get; private set; }

        public decimal? LastDistanceKm { get; private set; }

        public PricingResult Calculate(decimal distanceKm)
        {
            CalculateCount++;
            LastDistanceKm = distanceKm;
            return _innerService.Calculate(distanceKm);
        }
    }

    private sealed class CountingMapRouteService : IMapRouteService
    {
        public int CalculateCount { get; private set; }

        public Task<MapRouteResult> CalculateAsync(
            string originPlaceId,
            string destinationPlaceId,
            IReadOnlyList<string>? intermediatePlaceIds = null,
            CancellationToken cancellationToken = default)
        {
            CalculateCount++;

            return Task.FromResult(new MapRouteResult
            {
                DistanceMeters = intermediatePlaceIds is { Count: > 0 }
                    ? 12000
                    : 10000,
                Duration = intermediatePlaceIds is { Count: > 0 }
                    ? TimeSpan.FromMinutes(40)
                    : TimeSpan.FromMinutes(30)
            });
        }
    }

    private sealed class FakeRideRequestService : IRideRequestService
    {
        public int CreateCount { get; private set; }

        public RideRequestType? LastType { get; private set; }

        public DateOnly? LastRequestedDate { get; private set; }

        public decimal? LastSuggestedPrice { get; private set; }

        public Task<RideRequest> CreateAsync(
            string passengerRouteId,
            MatchResult match,
            RideRequestType type,
            DateOnly? requestedDate,
            decimal suggestedPrice,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            LastType = type;
            LastRequestedDate = requestedDate;
            LastSuggestedPrice = suggestedPrice;

            return Task.FromResult(new RideRequest
            {
                PassengerRouteId = passengerRouteId,
                DriverRouteId = match.DriverRoute.Id,
                Type = type,
                RequestedDate = requestedDate,
                SuggestedPrice = suggestedPrice
            });
        }

        public Task<bool> HasPendingRequestAsync(
            string passengerRouteId,
            string driverRouteId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<RideRequest>> GetMyPendingRequestsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RideRequest>>([]);

        public Task<IReadOnlyList<RideRequest>> GetMyActiveRequestsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RideRequest>>([]);

        public Task<IReadOnlyList<RideRequest>> GetReceivedPendingRequestsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RideRequest>>([]);

        public Task<IReadOnlyList<RideRequest>> GetMyAcceptedRequestsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RideRequest>>([]);

        public Task AcceptAsync(
            string requestId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RejectAsync(
            string requestId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
