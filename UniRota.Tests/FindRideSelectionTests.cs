using UniRota.Models;
using UniRota.Services.Interfaces;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class FindRideSelectionTests
{
    [Fact]
    public async Task Reload_PreservesSelectionByIdWithFreshRouteData()
    {
        var service = new RoutesStub();
        var viewModel = new FindRideViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        var previous = viewModel.PassengerRoutes[0];
        viewModel.SelectedRoute = previous;
        service.Routes = [new WeeklyRoute { Id = "passenger", Role = RouteRole.Passenger, Origin = "Endereço atualizado" }];

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanFindMatches);
        Assert.NotSame(previous, viewModel.SelectedRoute);
        Assert.Equal("Endereço atualizado", viewModel.SelectedRoute!.Route.Origin);
        WeeklyRouteItemViewModel? requested = null;
        viewModel.FindMatchesRequested += route => requested = route;
        viewModel.FindMatchesCommand.Execute(viewModel.SelectedRoute);
        Assert.Same(viewModel.SelectedRoute, requested);
    }

    [Fact]
    public async Task Reload_RemovedRouteClearsSelectionAndCannotStartMatching()
    {
        var service = new RoutesStub();
        var viewModel = new FindRideViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        var previous = viewModel.PassengerRoutes[0];
        viewModel.SelectedRoute = previous;
        service.Routes = [];

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Null(viewModel.SelectedRoute);
        Assert.False(viewModel.CanFindMatches);
        var navigations = 0;
        viewModel.FindMatchesRequested += _ => navigations++;
        viewModel.FindMatchesCommand.Execute(previous);
        Assert.Equal(0, navigations);
    }

    [Fact]
    public async Task FailedReload_DisablesSearchUntilDataCanBeValidatedAgain()
    {
        var service = new RoutesStub();
        var viewModel = new FindRideViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SelectedRoute = viewModel.PassengerRoutes[0];
        service.Fail = true;

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.False(viewModel.CanFindMatches);
        var navigations = 0;
        viewModel.FindMatchesRequested += _ => navigations++;
        viewModel.FindMatchesCommand.Execute(viewModel.SelectedRoute);
        Assert.Equal(0, navigations);
    }

    private sealed class RoutesStub : IRouteService
    {
        public IReadOnlyList<WeeklyRoute> Routes { get; set; } =
        [
            new WeeklyRoute { Id = "passenger", Role = RouteRole.Passenger },
            new WeeklyRoute { Id = "driver", Role = RouteRole.Driver }
        ];
        public bool Fail { get; set; }

        public Task<IReadOnlyList<WeeklyRoute>> GetMyRoutesAsync(CancellationToken cancellationToken = default)
            => Fail ? Task.FromException<IReadOnlyList<WeeklyRoute>>(new IOException("Offline")) : Task.FromResult(Routes);
        public Task<WeeklyRoute> CreateAsync(WeeklyRoute route, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WeeklyRoute> UpdateAsync(WeeklyRoute route, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string routeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WeeklyRoute>> GetDriverRoutesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
