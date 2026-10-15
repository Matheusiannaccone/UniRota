using UniRota.Models;
using UniRota.Services;

namespace UniRota.Tests;

public sealed class RouteSeatEditingRulesTests
{
    [Theory]
    [InlineData(3, 2, 1, 2)]
    [InlineData(5, 4, 3, 4)]
    [InlineData(2, 1, 0, 1)]
    public void Edit_PreservesWeeklyOccupation(int capacity, int monday, int tuesday, int wednesday)
    {
        var route = Driver();

        var result = RouteSeatEditingRules.GetUpdatedWeeklySeats(route, capacity, route.DaysOfWeek);

        Assert.Equal(monday, result[DayOfWeek.Monday]);
        Assert.Equal(tuesday, result[DayOfWeek.Tuesday]);
        Assert.Equal(wednesday, result[DayOfWeek.Wednesday]);
        Assert.Equal(2, route.WeeklyAvailableSeats[DayOfWeek.Monday]);
    }

    [Fact]
    public void Edit_RejectsReductionBelowWeeklyOccupation()
    {
        var route = Driver();

        Assert.Throws<ArgumentException>(() =>
            RouteSeatEditingRules.GetUpdatedWeeklySeats(route, 1, route.DaysOfWeek));
    }

    [Theory]
    [InlineData(2020, 1, 6)]
    [InlineData(2030, 1, 7)]
    public void Edit_RejectsReductionBelowWeeklyAndOnceOccupationIncludingHistory(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var route = Driver([Reservation("A", date), Reservation("B", date)]);

        Assert.Throws<ArgumentException>(() =>
            RouteSeatEditingRules.GetUpdatedWeeklySeats(route, 2, route.DaysOfWeek));
    }

    [Fact]
    public void Edit_DoesNotAccumulateOnceFromDifferentDates()
    {
        var route = Driver([
            Reservation("A", new DateOnly(2020, 1, 6)),
            Reservation("B", new DateOnly(2020, 1, 13))]);

        var result = RouteSeatEditingRules.GetUpdatedWeeklySeats(route, 2, route.DaysOfWeek);

        Assert.Equal(1, result[DayOfWeek.Monday]);
        Assert.Equal(2, route.OneTimeReservations.Count);
    }

    [Fact]
    public void Edit_NewDayStartsAtNewCapacity()
    {
        var route = Driver();

        var result = RouteSeatEditingRules.GetUpdatedWeeklySeats(
            route, 5, [.. route.DaysOfWeek, DayOfWeek.Friday]);

        Assert.Equal(5, result[DayOfWeek.Friday]);
        Assert.Equal(4, result[DayOfWeek.Monday]);
    }

    [Theory]
    [InlineData("free")]
    [InlineData("weekly")]
    [InlineData("historical-once")]
    [InlineData("future-once")]
    public void Edit_RemovesOnlyDaysWithoutCommitments(string scenario)
    {
        var route = new WeeklyRoute
        {
            Role = RouteRole.Driver,
            SeatCapacity = 3,
            DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday],
            WeeklyAvailableSeats = new Dictionary<DayOfWeek, int>
            {
                [DayOfWeek.Monday] = scenario == "weekly" ? 2 : 3,
                [DayOfWeek.Tuesday] = 3
            },
            OneTimeReservations = scenario switch
            {
                "historical-once" => [Reservation("A", new DateOnly(2020, 1, 6))],
                "future-once" => [Reservation("A", new DateOnly(2030, 1, 7))],
                _ => []
            }
        };

        if (scenario == "free")
        {
            var result = RouteSeatEditingRules.GetUpdatedWeeklySeats(route, 3, [DayOfWeek.Tuesday]);
            Assert.Single(result);
            Assert.False(result.ContainsKey(DayOfWeek.Monday));
        }
        else
        {
            Assert.Throws<ArgumentException>(() =>
                RouteSeatEditingRules.GetUpdatedWeeklySeats(route, 3, [DayOfWeek.Tuesday]));
        }
    }

    private static WeeklyRoute Driver(IReadOnlyList<OneTimeReservation>? reservations = null) => new()
    {
        Role = RouteRole.Driver,
        SeatCapacity = 3,
        DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
        WeeklyAvailableSeats = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 2,
            [DayOfWeek.Tuesday] = 1,
            [DayOfWeek.Wednesday] = 2
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
