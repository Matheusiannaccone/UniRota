using UniRota.Models;
using UniRota.Services.Interfaces;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class NewRouteViewModelTests
{
    [Fact]
    public async Task Save_CreatesDriverRouteWithDecimalDistance()
    {
        var service = new FakeRouteService();
        var viewModel = CreateValidViewModel(service, RouteRole.Driver);
        viewModel.EstimatedDistanceKmText = "12,75";

        await viewModel.SaveCommand.ExecuteAsync(null);

        var route = Assert.Single(service.CreatedRoutes);
        Assert.Equal(12.75m, route.EstimatedDistanceKm);
    }

    [Fact]
    public async Task Save_DoesNotCreateDriverRouteWithoutDistance()
    {
        var service = new FakeRouteService();
        var viewModel = CreateValidViewModel(service, RouteRole.Driver);
        viewModel.EstimatedDistanceKmText = string.Empty;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(service.CreatedRoutes);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task Save_DoesNotCreateDriverRouteWithZeroDistance()
    {
        var service = new FakeRouteService();
        var viewModel = CreateValidViewModel(service, RouteRole.Driver);
        viewModel.EstimatedDistanceKmText = "0";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(service.CreatedRoutes);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task Save_DoesNotCreateDriverRouteWithNegativeDistance()
    {
        var service = new FakeRouteService();
        var viewModel = CreateValidViewModel(service, RouteRole.Driver);
        viewModel.EstimatedDistanceKmText = "-8,5";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(service.CreatedRoutes);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task Save_DoesNotCreateDriverRouteWithInvalidDistanceFormat()
    {
        var service = new FakeRouteService();
        var viewModel = CreateValidViewModel(service, RouteRole.Driver);
        viewModel.EstimatedDistanceKmText = "oito e meio";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(service.CreatedRoutes);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task Save_CreatesPassengerRouteWithZeroDistance()
    {
        var service = new FakeRouteService();
        var viewModel = CreateValidViewModel(service, RouteRole.Passenger);
        viewModel.EstimatedDistanceKmText = "99,9";

        await viewModel.SaveCommand.ExecuteAsync(null);

        var route = Assert.Single(service.CreatedRoutes);
        Assert.Equal(0m, route.EstimatedDistanceKm);
    }

    [Fact]
    public void SelectingPassenger_ClearsDriverDistance()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedRole = GetRole(viewModel, RouteRole.Driver);
        viewModel.EstimatedDistanceKmText = "8,5";

        viewModel.SelectedRole = GetRole(viewModel, RouteRole.Passenger);

        Assert.Equal(string.Empty, viewModel.EstimatedDistanceKmText);
    }

    [Fact]
    public void BeginEdit_LoadsDriverDistanceUsingPtBrFormat()
    {
        var viewModel = CreateViewModel();

        viewModel.BeginEdit(CreateRoute(RouteRole.Driver, 8.5m));

        Assert.True(viewModel.IsDriver);
        Assert.Equal("8,5", viewModel.EstimatedDistanceKmText);
    }

    [Fact]
    public void BeginEdit_KeepsPassengerDistanceAtZeroAndHidden()
    {
        var viewModel = CreateViewModel();

        viewModel.BeginEdit(CreateRoute(RouteRole.Passenger, 0m));

        Assert.False(viewModel.IsDriver);
        Assert.Equal(string.Empty, viewModel.EstimatedDistanceKmText);
    }

    [Fact]
    public async Task Save_UpdatesDriverRouteWithNewDistanceAndPreservesId()
    {
        var service = new FakeRouteService();
        var viewModel = new NewRouteViewModel(service, new FakePlaceService());
        viewModel.BeginEdit(CreateRoute(RouteRole.Driver, 8.5m));
        viewModel.EstimatedDistanceKmText = "14,25";

        await viewModel.SaveCommand.ExecuteAsync(null);

        var route = Assert.Single(service.UpdatedRoutes);
        Assert.Equal("route-1", route.Id);
        Assert.Equal(14.25m, route.EstimatedDistanceKm);
        Assert.Empty(service.CreatedRoutes);
    }

    [Fact]
    public async Task Autocomplete_DoesNotSearchBelowMinimumLength()
    {
        var placeService = new FakePlaceService();
        var viewModel = CreateViewModel(placeService: placeService);

        viewModel.Origin = "ab";
        await Task.Delay(450);

        Assert.Equal(0, placeService.SearchCallCount);
        Assert.Empty(viewModel.OriginSuggestions);
    }

    [Fact]
    public async Task Autocomplete_DebouncesRapidInput()
    {
        var placeService = new FakePlaceService();
        var viewModel = CreateViewModel(placeService: placeService);

        viewModel.Origin = "Aven";
        await Task.Delay(100);
        viewModel.Origin = "Avenida";
        await Task.Delay(450);

        Assert.Equal(1, placeService.SearchCallCount);
        Assert.Equal("Avenida", Assert.Single(placeService.Queries));
    }

    [Fact]
    public async Task Autocomplete_NewQueryCancelsPreviousSearch()
    {
        var firstSearchStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSearchCancelled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var placeService = new FakePlaceService
        {
            SearchHandler = async (query, session, cancellationToken) =>
            {
                if (query == "Avenida")
                {
                    firstSearchStarted.SetResult();

                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        firstSearchCancelled.SetResult();
                        throw;
                    }
                }

                return [CreateSuggestion("new-place", "Avenida Nova")];
            }
        };
        var viewModel = CreateViewModel(placeService: placeService);

        viewModel.Origin = "Avenida";
        await firstSearchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        viewModel.Origin = "Avenida Nova";
        await firstSearchCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(450);

        Assert.Equal("Avenida Nova", Assert.Single(viewModel.OriginSuggestions).DisplayText);
    }

    [Fact]
    public async Task Autocomplete_PresentsSuggestions()
    {
        var placeService = new FakePlaceService
        {
            SearchResults =
            [
                CreateSuggestion("place-1", "Rua A, Sorocaba"),
                CreateSuggestion("place-2", "Rua B, Sorocaba")
            ]
        };
        var viewModel = CreateViewModel(placeService: placeService);

        viewModel.Origin = "Rua";
        await Task.Delay(450);

        Assert.Equal(2, viewModel.OriginSuggestions.Count);
        Assert.True(viewModel.HasOriginSuggestions);
    }

    [Fact]
    public async Task SelectingOrigin_FillsTextAndPlaceId()
    {
        var placeService = CreateSelectablePlaceService(
            "origin-id",
            "Rua Selecionada, Sorocaba");
        var viewModel = CreateViewModel(placeService: placeService);

        viewModel.Origin = "Rua";
        await Task.Delay(450);
        await viewModel.SelectOriginCommand.ExecuteAsync(
            Assert.Single(viewModel.OriginSuggestions));

        Assert.Equal("Rua Selecionada, Sorocaba", viewModel.Origin);
        Assert.Equal("origin-id", viewModel.OriginPlaceId);
        Assert.Empty(viewModel.OriginSuggestions);
    }

    [Fact]
    public async Task SelectingDestination_FillsTextAndPlaceId()
    {
        var placeService = CreateSelectablePlaceService(
            "destination-id",
            "Facens, Sorocaba");
        var viewModel = CreateViewModel(placeService: placeService);

        viewModel.Destination = "Fac";
        await Task.Delay(450);
        await viewModel.SelectDestinationCommand.ExecuteAsync(
            Assert.Single(viewModel.DestinationSuggestions));

        Assert.Equal("Facens, Sorocaba", viewModel.Destination);
        Assert.Equal("destination-id", viewModel.DestinationPlaceId);
        Assert.Empty(viewModel.DestinationSuggestions);
    }

    [Fact]
    public async Task Autocomplete_UsesIndependentOriginAndDestinationSessions()
    {
        var placeService = new FakePlaceService();
        var viewModel = CreateViewModel(placeService: placeService);

        viewModel.Origin = "Rua";
        viewModel.Destination = "Facens";
        await Task.Delay(450);

        Assert.Equal(2, placeService.Sessions.Count);
        Assert.NotEqual(
            placeService.Sessions[0].Id,
            placeService.Sessions[1].Id);
    }

    [Fact]
    public async Task EditingOriginAfterSelection_InvalidatesPlaceId()
    {
        var placeService = CreateSelectablePlaceService(
            "origin-id",
            "Rua Selecionada, Sorocaba");
        var viewModel = CreateViewModel(placeService: placeService);
        viewModel.Origin = "Rua";
        await Task.Delay(450);
        await viewModel.SelectOriginCommand.ExecuteAsync(
            Assert.Single(viewModel.OriginSuggestions));

        viewModel.Origin += " 12345";

        Assert.Equal(string.Empty, viewModel.OriginPlaceId);
    }

    [Fact]
    public async Task EditingDestinationAfterSelection_InvalidatesPlaceId()
    {
        var placeService = CreateSelectablePlaceService(
            "destination-id",
            "Facens, Sorocaba");
        var viewModel = CreateViewModel(placeService: placeService);
        viewModel.Destination = "Fac";
        await Task.Delay(450);
        await viewModel.SelectDestinationCommand.ExecuteAsync(
            Assert.Single(viewModel.DestinationSuggestions));

        viewModel.Destination += " alterada";

        Assert.Equal(string.Empty, viewModel.DestinationPlaceId);
    }

    [Fact]
    public async Task ClearingAddress_RemovesSelectionAndSuggestions()
    {
        var placeService = CreateSelectablePlaceService(
            "origin-id",
            "Rua Selecionada, Sorocaba");
        var viewModel = CreateViewModel(placeService: placeService);
        viewModel.Origin = "Rua";
        await Task.Delay(450);
        await viewModel.SelectOriginCommand.ExecuteAsync(
            Assert.Single(viewModel.OriginSuggestions));

        viewModel.Origin = string.Empty;

        Assert.Equal(string.Empty, viewModel.OriginPlaceId);
        Assert.Empty(viewModel.OriginSuggestions);
        Assert.False(viewModel.IsSearchingOrigin);
    }

    [Fact]
    public async Task Save_DoesNotCreateRouteWithFreeTextAddresses()
    {
        var routeService = new FakeRouteService();
        var viewModel = CreateValidViewModel(routeService, RouteRole.Passenger);
        viewModel.OriginPlaceId = string.Empty;
        viewModel.DestinationPlaceId = string.Empty;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(routeService.CreatedRoutes);
        Assert.Equal(
            "Selecione uma origem válida nas sugestões.",
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Save_CreatesRouteWithSelectedAddresses()
    {
        var routeService = new FakeRouteService();
        var viewModel = CreateValidViewModel(routeService, RouteRole.Passenger);

        await viewModel.SaveCommand.ExecuteAsync(null);

        var route = Assert.Single(routeService.CreatedRoutes);
        Assert.Equal("origin-place-id", route.OriginPlaceId);
        Assert.Equal("destination-place-id", route.DestinationPlaceId);
    }

    [Fact]
    public async Task BeginEdit_LoadsLegacyRouteButRequiresSelectionsToSave()
    {
        var routeService = new FakeRouteService();
        var viewModel = new NewRouteViewModel(
            routeService,
            new FakePlaceService());
        var legacyRoute = CreateRoute(
            RouteRole.Passenger,
            0m,
            includePlaceIds: false);

        viewModel.BeginEdit(legacyRoute);
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Centro", viewModel.Origin);
        Assert.Equal("Facens", viewModel.Destination);
        Assert.Empty(routeService.UpdatedRoutes);
        Assert.Equal(
            "Selecione uma origem válida nas sugestões.",
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Autocomplete_ServiceFailureDoesNotCrash()
    {
        var placeService = new FakePlaceService
        {
            SearchHandler = (query, session, cancellationToken) =>
                throw new InvalidOperationException("Serviço indisponível.")
        };
        var viewModel = CreateViewModel(placeService: placeService);

        viewModel.Origin = "Avenida";
        await Task.Delay(450);

        Assert.True(viewModel.HasOriginSearchError);
        Assert.Equal("Serviço indisponível.", viewModel.OriginSearchError);
        Assert.Empty(viewModel.OriginSuggestions);
    }

    [Fact]
    public void WeeklyRouteItem_ShowsDistanceOnlyForDriver()
    {
        var driverItem = new WeeklyRouteItemViewModel(
            CreateRoute(RouteRole.Driver, 12.5m));
        var passengerItem = new WeeklyRouteItemViewModel(
            CreateRoute(RouteRole.Passenger, 0m));

        Assert.True(driverItem.HasEstimatedDistance);
        Assert.Equal("Distância estimada: 12,5 km", driverItem.EstimatedDistanceText);
        Assert.False(passengerItem.HasEstimatedDistance);
        Assert.Equal(string.Empty, passengerItem.EstimatedDistanceText);
    }

    private static NewRouteViewModel CreateValidViewModel(
        FakeRouteService service,
        RouteRole role)
    {
        var viewModel = new NewRouteViewModel(service, new FakePlaceService())
        {
            SelectedRole = null,
            Origin = "Centro",
            Destination = "Facens",
            DepartureTime = new TimeSpan(7, 30, 0),
            AvailableSeats = role == RouteRole.Driver ? 2 : null,
            EstimatedDistanceKmText = role == RouteRole.Driver ? "8,5" : string.Empty
        };

        viewModel.OriginPlaceId = "origin-place-id";
        viewModel.DestinationPlaceId = "destination-place-id";

        viewModel.SelectedRole = GetRole(viewModel, role);
        viewModel.Days.Single(day => day.Day == DayOfWeek.Monday).IsSelected = true;
        return viewModel;
    }

    private static NewRouteViewModel CreateViewModel(
        FakeRouteService? routeService = null,
        FakePlaceService? placeService = null)
    {
        return new NewRouteViewModel(
            routeService ?? new FakeRouteService(),
            placeService ?? new FakePlaceService());
    }

    private static FakePlaceService CreateSelectablePlaceService(
        string placeId,
        string address)
    {
        return new FakePlaceService
        {
            SearchResults = [CreateSuggestion(placeId, address)],
            SelectedPlace = new SelectedPlace
            {
                PlaceId = placeId,
                Address = address
            }
        };
    }

    private static PlaceSuggestion CreateSuggestion(
        string placeId,
        string displayText)
    {
        return new PlaceSuggestion
        {
            PlaceId = placeId,
            DisplayText = displayText
        };
    }

    private static RouteRoleOption GetRole(
        NewRouteViewModel viewModel,
        RouteRole role)
    {
        return viewModel.RoleOptions.Single(option => option.Role == role);
    }

    private static WeeklyRoute CreateRoute(
        RouteRole role,
        decimal estimatedDistanceKm,
        bool includePlaceIds = true)
    {
        return new WeeklyRoute
        {
            Id = "route-1",
            UserId = "user-1",
            UserName = "Usuário",
            Role = role,
            Origin = "Centro",
            OriginPlaceId = includePlaceIds ? "origin-place-id" : string.Empty,
            Destination = "Facens",
            DestinationPlaceId = includePlaceIds
                ? "destination-place-id"
                : string.Empty,
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 450,
            AvailableSeats = role == RouteRole.Driver ? 2 : null,
            EstimatedDistanceKm = estimatedDistanceKm,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
    }

    private sealed class FakePlaceService : IPlaceService
    {
        public int SearchCallCount { get; private set; }

        public List<string> Queries { get; } = [];

        public List<PlaceAutocompleteSession> Sessions { get; } = [];

        public IReadOnlyList<PlaceSuggestion> SearchResults { get; init; } = [];

        public SelectedPlace SelectedPlace { get; init; } = new()
        {
            PlaceId = "selected-place-id",
            Address = "Endereço selecionado"
        };

        public Func<
            string,
            PlaceAutocompleteSession,
            CancellationToken,
            Task<IReadOnlyList<PlaceSuggestion>>>? SearchHandler { get; init; }

        public PlaceAutocompleteSession CreateSession()
        {
            var session = new PlaceAutocompleteSession(Guid.NewGuid().ToString());
            Sessions.Add(session);
            return session;
        }

        public Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(
            string query,
            PlaceAutocompleteSession session,
            CancellationToken cancellationToken = default)
        {
            ++SearchCallCount;
            Queries.Add(query);

            return SearchHandler is not null
                ? SearchHandler(query, session, cancellationToken)
                : Task.FromResult(SearchResults);
        }

        public Task<SelectedPlace> GetPlaceAsync(
            string placeId,
            PlaceAutocompleteSession session,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SelectedPlace);
        }
    }

    private sealed class FakeRouteService : IRouteService
    {
        public List<WeeklyRoute> CreatedRoutes { get; } = [];

        public List<WeeklyRoute> UpdatedRoutes { get; } = [];

        public Task<WeeklyRoute> CreateAsync(
            WeeklyRoute route,
            CancellationToken cancellationToken = default)
        {
            CreatedRoutes.Add(route);
            return Task.FromResult(route);
        }

        public Task<WeeklyRoute> UpdateAsync(
            WeeklyRoute route,
            CancellationToken cancellationToken = default)
        {
            UpdatedRoutes.Add(route);
            return Task.FromResult(route);
        }

        public Task DeleteAsync(
            string routeId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WeeklyRoute>> GetMyRoutesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WeeklyRoute>>([]);
        }

        public Task<IReadOnlyList<WeeklyRoute>> GetDriverRoutesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WeeklyRoute>>([]);
        }
    }
}
