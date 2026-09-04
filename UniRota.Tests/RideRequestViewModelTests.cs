using UniRota.Models;
using UniRota.Services;
using UniRota.Services.Interfaces;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class RideRequestViewModelTests
{
    [Fact]
    public void SetRequestContext_CalculatesAndExposesSuggestedPrice()
    {
        var requestService = new FakeRideRequestService();
        var pricingService = new CountingPricingService();
        var viewModel = new RideRequestViewModel(
            requestService,
            pricingService);
        var (passengerRoute, match) = CreateContext(12.75m);

        viewModel.SetRequestContext(passengerRoute, match);

        Assert.Equal(1, pricingService.CalculateCount);
        Assert.Equal(12.75m, pricingService.LastDistanceKm);
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
    public async Task InvalidDriverDistance_PreventsRequestCreation()
    {
        var requestService = new FakeRideRequestService();
        var pricingService = new CountingPricingService();
        var viewModel = new RideRequestViewModel(
            requestService,
            pricingService);
        var (passengerRoute, match) = CreateContext(0m);

        viewModel.SetRequestContext(passengerRoute, match);

        Assert.False(viewModel.HasSuggestedPrice);
        Assert.True(viewModel.HasError);
        Assert.Contains("distância estimada válida", viewModel.ErrorMessage);

        viewModel.SelectedRequestType = GetRequestType(
            viewModel,
            RideRequestType.Weekly);
        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(1, pricingService.CalculateCount);
        Assert.Equal(0, requestService.CreateCount);
        Assert.True(viewModel.HasError);
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
        decimal estimatedDistanceKm)
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
            EstimatedDistanceKm = estimatedDistanceKm
        };

        return (
            passengerRoute,
            new MatchResult(driverRoute, [currentDay], 0));
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
