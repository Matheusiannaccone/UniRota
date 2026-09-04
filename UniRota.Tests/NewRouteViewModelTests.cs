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
        var viewModel = new NewRouteViewModel(new FakeRouteService());
        viewModel.SelectedRole = GetRole(viewModel, RouteRole.Driver);
        viewModel.EstimatedDistanceKmText = "8,5";

        viewModel.SelectedRole = GetRole(viewModel, RouteRole.Passenger);

        Assert.Equal(string.Empty, viewModel.EstimatedDistanceKmText);
    }

    [Fact]
    public void BeginEdit_LoadsDriverDistanceUsingPtBrFormat()
    {
        var viewModel = new NewRouteViewModel(new FakeRouteService());

        viewModel.BeginEdit(CreateRoute(RouteRole.Driver, 8.5m));

        Assert.True(viewModel.IsDriver);
        Assert.Equal("8,5", viewModel.EstimatedDistanceKmText);
    }

    [Fact]
    public void BeginEdit_KeepsPassengerDistanceAtZeroAndHidden()
    {
        var viewModel = new NewRouteViewModel(new FakeRouteService());

        viewModel.BeginEdit(CreateRoute(RouteRole.Passenger, 0m));

        Assert.False(viewModel.IsDriver);
        Assert.Equal(string.Empty, viewModel.EstimatedDistanceKmText);
    }

    [Fact]
    public async Task Save_UpdatesDriverRouteWithNewDistanceAndPreservesId()
    {
        var service = new FakeRouteService();
        var viewModel = new NewRouteViewModel(service);
        viewModel.BeginEdit(CreateRoute(RouteRole.Driver, 8.5m));
        viewModel.EstimatedDistanceKmText = "14,25";

        await viewModel.SaveCommand.ExecuteAsync(null);

        var route = Assert.Single(service.UpdatedRoutes);
        Assert.Equal("route-1", route.Id);
        Assert.Equal(14.25m, route.EstimatedDistanceKm);
        Assert.Empty(service.CreatedRoutes);
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
        var viewModel = new NewRouteViewModel(service)
        {
            SelectedRole = null,
            Origin = "Centro",
            Destination = "Facens",
            DepartureTime = new TimeSpan(7, 30, 0),
            AvailableSeats = role == RouteRole.Driver ? 2 : null,
            EstimatedDistanceKmText = role == RouteRole.Driver ? "8,5" : string.Empty
        };

        viewModel.SelectedRole = GetRole(viewModel, role);
        viewModel.Days.Single(day => day.Day == DayOfWeek.Monday).IsSelected = true;
        return viewModel;
    }

    private static RouteRoleOption GetRole(
        NewRouteViewModel viewModel,
        RouteRole role)
    {
        return viewModel.RoleOptions.Single(option => option.Role == role);
    }

    private static WeeklyRoute CreateRoute(
        RouteRole role,
        decimal estimatedDistanceKm)
    {
        return new WeeklyRoute
        {
            Id = "route-1",
            UserId = "user-1",
            UserName = "Usuário",
            Role = role,
            Origin = "Centro",
            Destination = "Facens",
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 450,
            AvailableSeats = role == RouteRole.Driver ? 2 : null,
            EstimatedDistanceKm = estimatedDistanceKm,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
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
