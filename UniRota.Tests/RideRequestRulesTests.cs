using UniRota.Models;
using UniRota.Services;

namespace UniRota.Tests;

public sealed class RideRequestRulesTests
{
    private static readonly DateOnly CurrentDate = new(2026, 9, 2);

    [Fact]
    public void ValidateForCreation_AcceptsOnceOnCompatibleFutureDate()
    {
        var requestedDate = new DateOnly(2026, 9, 4);

        var days = RideRequestRules.ValidateForCreation(
            RideRequestType.Once,
            [DayOfWeek.Monday, DayOfWeek.Friday],
            requestedDate,
            CurrentDate);

        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Friday],
            days);
    }

    [Fact]
    public void ValidateForCreation_RejectsOnceWithoutDate()
    {
        Assert.Throws<ArgumentException>(() =>
            RideRequestRules.ValidateForCreation(
                RideRequestType.Once,
                [DayOfWeek.Friday],
                null,
                CurrentDate));
    }

    [Fact]
    public void ValidateForCreation_RejectsOnceInThePast()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RideRequestRules.ValidateForCreation(
                RideRequestType.Once,
                [DayOfWeek.Monday],
                new DateOnly(2026, 8, 31),
                CurrentDate));
    }

    [Fact]
    public void ValidateForCreation_RejectsOnceOnIncompatibleDay()
    {
        Assert.Throws<ArgumentException>(() =>
            RideRequestRules.ValidateForCreation(
                RideRequestType.Once,
                [DayOfWeek.Monday, DayOfWeek.Friday],
                new DateOnly(2026, 9, 3),
                CurrentDate));
    }

    [Fact]
    public void ValidateForCreation_AcceptsWeeklyWithoutDateAndNormalizesDays()
    {
        var days = RideRequestRules.ValidateForCreation(
            RideRequestType.Weekly,
            [DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Friday],
            null,
            CurrentDate);

        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Friday],
            days);
    }

    [Fact]
    public void ValidateForCreation_RejectsWeeklyWithDate()
    {
        Assert.Throws<ArgumentException>(() =>
            RideRequestRules.ValidateForCreation(
                RideRequestType.Weekly,
                [DayOfWeek.Friday],
                new DateOnly(2026, 9, 4),
                CurrentDate));
    }

    [Fact]
    public void EnsurePending_AcceptsPendingStatus()
    {
        RideRequestRules.EnsurePending(RideRequestStatus.Pending);
    }

    [Theory]
    [InlineData(RideRequestStatus.Accepted)]
    [InlineData(RideRequestStatus.Rejected)]
    public void EnsurePending_RejectsProcessedStatus(RideRequestStatus status)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => RideRequestRules.EnsurePending(status));

        Assert.Equal("Esta solicitação já foi processada.", exception.Message);
    }

    [Fact]
    public void ApplyWeeklyAcceptance_DoesNotMutateInputOnReservationConflict()
    {
        var weekly = new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 1 };
        var reservations = new[] { new OneTimeReservation
        {
            RequestId = "once", Date = new DateOnly(2026, 9, 7), DayOfWeek = DayOfWeek.Monday
        } };

        Assert.Throws<InvalidOperationException>(() => RideRequestRules.ApplyWeeklyAcceptance(
            weekly, reservations, [DayOfWeek.Monday], CurrentDate));

        Assert.Equal(1, weekly[DayOfWeek.Monday]);
        Assert.Single(reservations);
    }

    [Theory]
    [InlineData(-7, true)]
    [InlineData(0, false)]
    [InlineData(7, false)]
    public void ApplyWeeklyAcceptance_ConsidersOnlyTodayAndFutureReservations(int offset, bool allowed)
    {
        var today = new DateOnly(2026, 10, 19);
        var weekly = new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 1 };
        var date = today.AddDays(offset);
        var reservations = new[] { new OneTimeReservation
        {
            RequestId = "once", Date = date, DayOfWeek = date.DayOfWeek
        } };

        if (allowed)
        {
            var updated = RideRequestRules.ApplyWeeklyAcceptance(
                weekly, reservations, [DayOfWeek.Monday], today);
            Assert.Equal(0, updated.WeeklySeats[DayOfWeek.Monday]);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => RideRequestRules.ApplyWeeklyAcceptance(
                weekly, reservations, [DayOfWeek.Monday], today));
        }

        Assert.Equal(1, weekly[DayOfWeek.Monday]);
        Assert.Single(reservations);
    }

    [Fact]
    public void ApplyWeeklyAcceptance_DoesNotSumDifferentDatesOfSameWeekday()
    {
        var today = new DateOnly(2026, 10, 19);
        var weekly = new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 2 };
        var reservations = new[] { today, today.AddDays(7), today.AddDays(14) }
            .Select((date, index) => new OneTimeReservation
            {
                RequestId = "once-" + index, Date = date, DayOfWeek = date.DayOfWeek
            }).ToArray();

        var updated = RideRequestRules.ApplyWeeklyAcceptance(
            weekly, reservations, [DayOfWeek.Monday], today);

        Assert.Equal(1, updated.WeeklySeats[DayOfWeek.Monday]);
        Assert.Equal(3, reservations.Length);
    }

    [Fact]
    public void HasSeatsOnDate_CountsOnlyExactDateIncludingWhenOtherDatesArePast()
    {
        var date = new DateOnly(2026, 10, 19);
        var weekly = new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 1 };
        var reservations = new[] { date.AddDays(-7), date.AddDays(7) }
            .Select((otherDate, index) => new OneTimeReservation
            {
                RequestId = "once-" + index, Date = otherDate, DayOfWeek = otherDate.DayOfWeek
            }).ToArray();

        Assert.True(RideRequestRules.HasSeatsOnDate(weekly, reservations, date));
        Assert.False(RideRequestRules.HasSeatsOnDate(weekly, reservations, date.AddDays(7)));
    }

    [Fact]
    public void ApplyWeeklyAcceptance_ReturnsDistinctSubsetAndConsumesOnlyGrantedDays()
    {
        var today = new DateOnly(2026, 10, 19);
        var weekly = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 0, [DayOfWeek.Tuesday] = 2, [DayOfWeek.Wednesday] = 3
        };
        var result = RideRequestRules.ApplyWeeklyAcceptance(weekly, [],
            [DayOfWeek.Tuesday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Friday], today);

        Assert.Equal(new[] { DayOfWeek.Tuesday }, result.AcceptedDays);
        Assert.Equal(0, result.WeeklySeats[DayOfWeek.Monday]);
        Assert.Equal(1, result.WeeklySeats[DayOfWeek.Tuesday]);
        Assert.Equal(3, result.WeeklySeats[DayOfWeek.Wednesday]);
        Assert.Equal(2, weekly[DayOfWeek.Tuesday]);
    }
}
