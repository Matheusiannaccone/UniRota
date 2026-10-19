using UniRota.Models;
using UniRota.Services;

namespace UniRota.Tests;

public sealed class PassengerScheduleRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    [Theory]
    [InlineData(-7, true)]
    [InlineData(0, false)]
    [InlineData(7, false)]
    public void WeeklyFreeDays_IgnorePastOnceAndBlockCurrentOrFutureWeekday(int offset, bool mondayFree)
    {
        var passenger = new WeeklyRoute
        {
            Role = RouteRole.Passenger,
            DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
            WeeklyAcceptedDays = [DayOfWeek.Tuesday],
            OneTimeAcceptedDates = [Today.AddDays(offset)]
        };

        var days = RideRequestRules.GetPassengerAvailableWeeklyDays(passenger, passenger.DaysOfWeek, Today);

        Assert.Equal(mondayFree ? new[] { DayOfWeek.Monday, DayOfWeek.Wednesday } : [DayOfWeek.Wednesday], days);
        Assert.Equal(new[] { Today.AddDays(offset) }, passenger.OneTimeAcceptedDates);
        Assert.Equal(3, passenger.DaysOfWeek.Count);
    }

    [Theory]
    [InlineData("free", true)]
    [InlineData("past", false)]
    [InlineData("weekly", false)]
    [InlineData("same-date", false)]
    [InlineData("other-date", true)]
    public void OnceAvailability_UsesRecurringDaysAndExactDatesOnly(string scenario, bool expected)
    {
        var passenger = new WeeklyRoute
        {
            WeeklyAcceptedDays = scenario == "weekly" ? [DayOfWeek.Monday] : [],
            OneTimeAcceptedDates = scenario == "same-date" ? [Today]
                : scenario == "other-date" ? [Today.AddDays(7)] : []
        };
        var date = scenario == "past" ? Today.AddDays(-1) : Today;
        Assert.Equal(expected, RideRequestRules.CanPassengerAcceptOnce(passenger, date, Today));
        Assert.Equal(expected, RideRequestRules.GetPassengerOnceConflict(passenger, date, Today) is null);
    }
}
