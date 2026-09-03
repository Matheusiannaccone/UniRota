using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class ConfirmedRideItemViewModelTests
{
    [Fact]
    public void Constructor_IdentifiesPassengerByUserId()
    {
        var request = CreateRequest();

        var item = new ConfirmedRideItemViewModel(
            request,
            request.PassengerUserId);

        Assert.Equal("Você é o passageiro", item.CurrentUserRoleText);
    }

    [Fact]
    public void Constructor_IdentifiesDriverByUserId()
    {
        var request = CreateRequest();

        var item = new ConfirmedRideItemViewModel(
            request,
            request.DriverUserId);

        Assert.Equal("Você é o motorista", item.CurrentUserRoleText);
    }

    [Fact]
    public void Constructor_RejectsUnrelatedUser()
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentException>(() =>
            new ConfirmedRideItemViewModel(request, "other-user"));
    }

    [Fact]
    public void Constructor_UsesNameFallbacksForOldDocuments()
    {
        var request = CreateRequest(
            passengerUserName: string.Empty,
            driverUserName: " ");

        var item = new ConfirmedRideItemViewModel(
            request,
            request.PassengerUserId);

        Assert.Equal("Passageiro", item.PassengerNameText);
        Assert.Equal("Motorista", item.DriverNameText);
    }

    [Fact]
    public void OnceRequest_ShowsStoredDateWithoutChangingIt()
    {
        var requestedDate = new DateOnly(2026, 9, 4);
        var request = CreateRequest(
            type: RideRequestType.Once,
            requestedDate: requestedDate);

        var item = new ConfirmedRideItemViewModel(
            request,
            request.PassengerUserId);

        Assert.Equal("Uma vez", item.TypeText);
        Assert.True(item.HasRequestedDate);
        Assert.Equal("04/09/2026", item.RequestedDateText);
        Assert.Equal(requestedDate, item.Request.RequestedDate);
    }

    [Fact]
    public void WeeklyRequest_ShowsStoredCompatibleDaysWithoutDate()
    {
        var request = CreateRequest(
            compatibleDays: [DayOfWeek.Monday, DayOfWeek.Friday]);

        var item = new ConfirmedRideItemViewModel(
            request,
            request.DriverUserId);

        Assert.Equal("Semanal", item.TypeText);
        Assert.Equal("Segunda-feira, Sexta-feira", item.CompatibleDaysText);
        Assert.False(item.HasRequestedDate);
        Assert.Equal("Confirmada", item.StatusText);
    }

    private static RideRequest CreateRequest(
        string passengerUserName = "Ana",
        string driverUserName = "Bruno",
        IReadOnlyList<DayOfWeek>? compatibleDays = null,
        RideRequestType type = RideRequestType.Weekly,
        DateOnly? requestedDate = null)
    {
        return new RideRequest
        {
            Id = "request-1",
            PassengerUserId = "passenger-1",
            PassengerUserName = passengerUserName,
            DriverUserId = "driver-1",
            DriverUserName = driverUserName,
            PassengerRouteId = "passenger-route-1",
            DriverRouteId = "driver-route-1",
            CompatibleDays = compatibleDays ?? [DayOfWeek.Friday],
            Type = type,
            Status = RideRequestStatus.Accepted,
            RequestedDate = requestedDate,
            CreatedAtUtc = new DateTimeOffset(
                2026,
                9,
                2,
                12,
                0,
                0,
                TimeSpan.Zero)
        };
    }
}
