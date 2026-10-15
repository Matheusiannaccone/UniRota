using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class DriverWeeklyScheduleTests
{
    [Fact]
    public void Week_StartsOnSundayAndContainsSevenConsecutiveDates()
    {
        var item = new WeeklyRouteItemViewModel(Driver(), new DateOnly(2026, 10, 29));

        Assert.Equal(new DateOnly(2026, 10, 25), item.WeekStart);
        Assert.Equal("25/10/2026 – 31/10/2026", item.WeekRangeText);
        Assert.Equal(7, item.WeekDays.Count);
        Assert.Equal(new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" },
            item.WeekDays.Select(day => day.DisplayDay));
        Assert.Equal(Enumerable.Range(0, 7).Select(offset => item.WeekStart.AddDays(offset)),
            item.WeekDays.Select(day => day.Date));
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday, "-", null)]
    [InlineData(DayOfWeek.Monday, "1", 1)]
    [InlineData(DayOfWeek.Friday, "0", 0)]
    public void Week_DistinguishesOfferedDaysAndWeeklyPassengers(DayOfWeek day, string display, int? scheduled)
    {
        var item = new WeeklyRouteItemViewModel(Driver(), new DateOnly(2026, 10, 29));

        var entry = Assert.Single(item.WeekDays, entry => entry.DayOfWeek == day);

        Assert.Equal(display, entry.DisplayValue);
        Assert.Equal(scheduled, entry.ScheduledPassengers);
        Assert.Equal(scheduled.HasValue, entry.IsOfferedDay);
    }

    [Theory]
    [InlineData(22, 1)]
    [InlineData(29, 2)]
    [InlineData(36, 1)]
    public void Once_AppearsOnlyInItsExactWeek(int octoberDayOffset, int scheduled)
    {
        var route = Driver([Reservation("A", new DateOnly(2026, 10, 29))]);
        var reference = new DateOnly(2026, 10, 1).AddDays(octoberDayOffset - 1);
        var item = new WeeklyRouteItemViewModel(route, reference);

        var thursday = Assert.Single(item.WeekDays, entry => entry.DayOfWeek == DayOfWeek.Thursday);

        Assert.Equal(scheduled, thursday.ScheduledPassengers);
    }

    [Fact]
    public void Once_TwoReservationsOnTheSameDateAreAddedToWeeklyOccupation()
    {
        var date = new DateOnly(2026, 10, 29);
        var item = new WeeklyRouteItemViewModel(Driver([
            Reservation("A", date), Reservation("B", date)]), date);

        Assert.Equal(3, item.WeekDays.Single(entry => entry.Date == date).ScheduledPassengers);
    }

    [Fact]
    public void Once_OnDifferentDatesOfTheSameWeekdayStaysInItsRespectiveWeek()
    {
        var first = new DateOnly(2026, 10, 29);
        var second = new DateOnly(2026, 11, 5);
        var item = new WeeklyRouteItemViewModel(Driver([
            Reservation("A", first), Reservation("B", second), Reservation("C", second)]), first);

        Assert.Equal(2, item.WeekDays.Single(entry => entry.Date == first).ScheduledPassengers);
        item.NextWeekCommand.Execute(null);
        Assert.Equal(3, item.WeekDays.Single(entry => entry.Date == second).ScheduledPassengers);
    }

    [Fact]
    public void Navigation_RecalculatesDaysAndNotifiesBindingsInBothDirections()
    {
        var item = new WeeklyRouteItemViewModel(Driver([
            Reservation("A", new DateOnly(2026, 10, 29))]), new DateOnly(2026, 10, 29));
        var initialDays = item.WeekDays;
        var notifications = new List<string?>();
        item.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        item.NextWeekCommand.Execute(null);

        Assert.Equal(new DateOnly(2026, 11, 1), item.WeekStart);
        Assert.Equal(7, item.WeekDays.Count);
        Assert.NotSame(initialDays, item.WeekDays);
        Assert.Equal(1, item.WeekDays.Single(entry => entry.DayOfWeek == DayOfWeek.Thursday).ScheduledPassengers);
        item.PreviousWeekCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 10, 25), item.WeekStart);
        Assert.Equal(2, item.WeekDays.Single(entry => entry.DayOfWeek == DayOfWeek.Thursday).ScheduledPassengers);
        Assert.Equal(2, notifications.Count(name => name == nameof(item.WeekDays)));
        Assert.Equal(2, notifications.Count(name => name == nameof(item.WeekRangeText)));
    }

    [Fact]
    public void Passenger_HasNoDriverWeekOrWeekNavigation()
    {
        var item = new WeeklyRouteItemViewModel(new WeeklyRoute
        {
            Role = RouteRole.Passenger,
            DaysOfWeek = [DayOfWeek.Monday]
        }, new DateOnly(2026, 10, 29));

        Assert.False(item.HasDriverWeek);
        Assert.Empty(item.WeekDays);
        Assert.False(item.NextWeekCommand.CanExecute(null));
        Assert.False(item.PreviousWeekCommand.CanExecute(null));
    }

    [Fact]
    public void Navigation_HandlesYearBoundary()
    {
        var item = new WeeklyRouteItemViewModel(Driver(), new DateOnly(2026, 12, 31));

        item.NextWeekCommand.Execute(null);

        Assert.Equal(new DateOnly(2027, 1, 3), item.WeekStart);
        Assert.Equal(new DateOnly(2027, 1, 9), item.WeekDays[^1].Date);
        item.PreviousWeekCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 12, 27), item.WeekStart);
    }

    private static WeeklyRoute Driver(IReadOnlyList<OneTimeReservation>? reservations = null) => new()
    {
        Role = RouteRole.Driver,
        SeatCapacity = 3,
        DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Friday],
        WeeklyAvailableSeats = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 2,
            [DayOfWeek.Thursday] = 2,
            [DayOfWeek.Friday] = 3
        },
        OneTimeReservations = reservations ?? []
    };

    private static OneTimeReservation Reservation(string id, DateOnly date) => new()
    {
        RequestId = id,
        Date = date,
        DayOfWeek = date.DayOfWeek
    };
}
