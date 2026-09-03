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

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public void GetRemainingSeatsAfterAcceptance_DecrementsExactlyOneSeat(
        int availableSeats,
        int expectedRemainingSeats)
    {
        Assert.Equal(
            expectedRemainingSeats,
            RideRequestRules.GetRemainingSeatsAfterAcceptance(availableSeats));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetRemainingSeatsAfterAcceptance_RejectsUnavailableSeats(
        int? availableSeats)
    {
        Assert.Throws<InvalidOperationException>(() =>
            RideRequestRules.GetRemainingSeatsAfterAcceptance(availableSeats));
    }
}
