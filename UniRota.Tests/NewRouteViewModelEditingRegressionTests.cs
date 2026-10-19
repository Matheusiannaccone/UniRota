using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class NewRouteViewModelTests
{
    [Theory]
    [InlineData(RouteRole.Driver, 4, false, false)]
    [InlineData(RouteRole.Driver, 2, false, false)]
    [InlineData(RouteRole.Driver, 3, true, false)]
    [InlineData(RouteRole.Driver, 3, false, true)]
    [InlineData(RouteRole.Driver, 4, true, true)]
    [InlineData(RouteRole.Passenger, 0, true, false)]
    [InlineData(RouteRole.Passenger, 0, false, true)]
    public async Task Save_UnoccupiedRouteRespectsEditedCapacityAndDays(
        RouteRole role, int capacity, bool addThursday, bool removeWednesday)
    {
        var service = new FakeRouteService();
        var viewModel = CreateViewModel(routeService: service);
        var original = new WeeklyRoute
        {
            Id = "unoccupied",
            Role = role,
            Origin = "Centro",
            OriginPlaceId = "origin-place-id",
            Destination = "Facens",
            DestinationPlaceId = "destination-place-id",
            DepartureTimeMinutes = 450,
            DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
            SeatCapacity = role == RouteRole.Driver ? 3 : null,
            WeeklyAvailableSeats = role == RouteRole.Driver
                ? new Dictionary<DayOfWeek, int>
                {
                    [DayOfWeek.Monday] = 3,
                    [DayOfWeek.Tuesday] = 3,
                    [DayOfWeek.Wednesday] = 3
                }
                : new Dictionary<DayOfWeek, int>(),
            OneTimeReservations = [],
            EstimatedDistanceKm = role == RouteRole.Driver ? 8.5m : 0m
        };
        viewModel.BeginEdit(original);
        Assert.Equal(original.DaysOfWeek, viewModel.Days.Where(day => day.IsSelected).Select(day => day.Day));
        viewModel.SeatCapacity = role == RouteRole.Driver ? capacity : null;
        viewModel.Days.Single(day => day.Day == DayOfWeek.Thursday).IsSelected = addThursday;
        viewModel.Days.Single(day => day.Day == DayOfWeek.Wednesday).IsSelected = !removeWednesday;
        var selectedDays = viewModel.Days.Where(day => day.IsSelected).Select(day => day.Day).ToArray();

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasError, viewModel.ErrorMessage);
        var sent = Assert.Single(service.UpdatedRoutes);
        Assert.Equal(selectedDays, sent.DaysOfWeek);
        Assert.Equal(role == RouteRole.Driver ? capacity : (int?)null, sent.SeatCapacity);
        Assert.Empty(sent.OneTimeReservations);
        Assert.Null(sent.AvailableSeats);

        if (role == RouteRole.Driver)
        {
            Assert.Equal(selectedDays.Length, sent.WeeklyAvailableSeats.Count);
            Assert.All(selectedDays, day => Assert.Equal(capacity, sent.WeeklyAvailableSeats[day]));
        }
        else
        {
            Assert.Empty(sent.WeeklyAvailableSeats);
        }
    }
}
