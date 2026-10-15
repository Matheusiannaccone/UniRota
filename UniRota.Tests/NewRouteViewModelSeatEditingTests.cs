using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed partial class NewRouteViewModelTests
{
    [Theory]
    [InlineData(RouteRole.Driver)]
    [InlineData(RouteRole.Passenger)]
    public async Task Save_InitializesNewSeatStateWithoutLegacyBalance(RouteRole role)
    {
        var service = new FakeRouteService();
        var viewModel = CreateValidViewModel(service, role);
        viewModel.SeatCapacity = role == RouteRole.Driver ? 3 : null;
        viewModel.Days.Single(day => day.Day == DayOfWeek.Wednesday).IsSelected = true;
        viewModel.Days.Single(day => day.Day == DayOfWeek.Friday).IsSelected = true;

        await viewModel.SaveCommand.ExecuteAsync(null);

        var route = Assert.Single(service.CreatedRoutes);
        Assert.Null(route.AvailableSeats);
        Assert.Empty(route.OneTimeReservations);
        Assert.Equal(role == RouteRole.Driver ? 3 : (int?)null, route.SeatCapacity);

        if (role == RouteRole.Driver)
        {
            Assert.Equal(3, route.WeeklyAvailableSeats.Count);
            Assert.All(route.DaysOfWeek, day => Assert.Equal(3, route.WeeklyAvailableSeats[day]));
        }
        else
        {
            Assert.Empty(route.WeeklyAvailableSeats);
            Assert.False(viewModel.IsDriver);
        }
    }

    [Theory]
    [InlineData(3, 2, 1, 2)]
    [InlineData(5, 4, 3, 4)]
    [InlineData(2, 1, 0, 1)]
    public async Task Save_EditAndDistanceHelperPreserveOccupationAndMetadata(
        int capacity, int monday, int tuesday, int wednesday)
    {
        var service = new FakeRouteService();
        var viewModel = CreateViewModel(routeService: service);
        var original = OccupiedEditingRoute();
        viewModel.BeginEdit(original);
        Assert.Equal(3, viewModel.SeatCapacity);
        viewModel.SeatCapacity = capacity;

        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(service.UpdatedRoutes);
        Assert.Equal(capacity, saved.SeatCapacity);
        Assert.Equal(monday, saved.WeeklyAvailableSeats[DayOfWeek.Monday]);
        Assert.Equal(tuesday, saved.WeeklyAvailableSeats[DayOfWeek.Tuesday]);
        Assert.Equal(wednesday, saved.WeeklyAvailableSeats[DayOfWeek.Wednesday]);
        Assert.Equal(original.OneTimeReservations.ToArray(), saved.OneTimeReservations.ToArray());
        Assert.Equal(original.Id, saved.Id);
        Assert.Equal(original.UserId, saved.UserId);
        Assert.Equal(original.UserName, saved.UserName);
        Assert.Equal(original.RequestRevision, saved.RequestRevision);
        Assert.Equal(original.CreatedAtUtc, saved.CreatedAtUtc);
        Assert.Null(saved.AvailableSeats);
    }

    [Fact]
    public async Task Save_RejectsUnsafeEditBeforeMapCalculationOrWrite()
    {
        var service = new FakeRouteService();
        var maps = new FakeMapRouteService();
        var viewModel = CreateViewModel(routeService: service, mapRouteService: maps);
        viewModel.BeginEdit(OccupiedEditingRoute());
        viewModel.SeatCapacity = 1;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Empty(service.UpdatedRoutes);
        Assert.Empty(maps.Requests);
    }

    [Theory]
    [InlineData(RouteRole.Driver, RouteRole.Passenger)]
    [InlineData(RouteRole.Passenger, RouteRole.Driver)]
    public void BeginEdit_LocksRoleEvenForProgrammaticChanges(RouteRole original, RouteRole attempted)
    {
        var viewModel = CreateViewModel();
        viewModel.BeginEdit(CreateRoute(original, original == RouteRole.Driver ? 8.5m : 0m));

        viewModel.SelectedRole = GetRole(viewModel, attempted);

        Assert.False(viewModel.IsRoleEditable);
        Assert.Equal(original, viewModel.SelectedRole!.Role);
        Assert.True(viewModel.HasError);
        viewModel.BeginCreate();
        Assert.True(viewModel.IsRoleEditable);
    }

    private static WeeklyRoute OccupiedEditingRoute() => new()
    {
        Id = "occupied-route",
        UserId = "owner",
        UserName = "Motorista",
        Role = RouteRole.Driver,
        Origin = "Centro",
        OriginPlaceId = "origin-place-id",
        Destination = "Facens",
        DestinationPlaceId = "destination-place-id",
        DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
        DepartureTimeMinutes = 450,
        SeatCapacity = 3,
        WeeklyAvailableSeats = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 2,
            [DayOfWeek.Tuesday] = 1,
            [DayOfWeek.Wednesday] = 2
        },
        OneTimeReservations = [new OneTimeReservation
        {
            RequestId = "historical",
            Date = new DateOnly(2020, 1, 6),
            DayOfWeek = DayOfWeek.Monday
        }],
        RequestRevision = 7,
        CreatedAtUtc = DateTimeOffset.UnixEpoch,
        EstimatedDistanceKm = 8.5m
    };
}
