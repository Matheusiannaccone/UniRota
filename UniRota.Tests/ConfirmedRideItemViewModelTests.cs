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
    public void Passenger_UsesSuggestedPriceSnapshotFormattedPerTrip()
    {
        var request = CreateRequest(suggestedPrice: 7.89m);

        var item = new ConfirmedRideItemViewModel(
            request,
            request.PassengerUserId);

        Assert.Equal(7.89m, item.SuggestedPrice);
        Assert.Equal("Preço sugerido: R$ 7,89 por viagem", item.SuggestedPriceText);
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
    public void Driver_UsesSuggestedPriceSnapshotAsEstimatedAmountPerTrip()
    {
        var request = CreateRequest(suggestedPrice: 7.89m);

        var item = new ConfirmedRideItemViewModel(
            request,
            request.DriverUserId);

        Assert.Equal(7.89m, item.SuggestedPrice);
        Assert.Equal(
            "Valor estimado a receber: R$ 7,89 por viagem",
            item.SuggestedPriceText);
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
        Assert.Equal("Preço sugerido: R$ 3,45 por viagem", item.SuggestedPriceText);
    }

    [Fact]
    public void WeeklyRequest_ShowsAcceptedDaysWithoutDate()
    {
        var request = CreateRequest(
            compatibleDays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            acceptedDays: [DayOfWeek.Monday, DayOfWeek.Friday]);

        var item = new ConfirmedRideItemViewModel(
            request,
            request.DriverUserId);

        Assert.Equal("Semanal", item.TypeText);
        Assert.Equal("Segunda-feira, Sexta-feira", item.CompatibleDaysText);
        Assert.False(item.HasRequestedDate);
        Assert.Equal(
            "Valor estimado a receber: R$ 3,45 por viagem",
            item.SuggestedPriceText);
        Assert.Equal("Confirmada", item.StatusText);
    }

    [Theory]
    [InlineData("passenger-1")]
    [InlineData("driver-1")]
    public void WeeklyPartialAcceptance_ShowsOnlyGrantedDaysInConsistentOrder(string currentUserId)
    {
        var compatible = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };
        var accepted = new[] { DayOfWeek.Wednesday, DayOfWeek.Tuesday };
        var request = CreateRequest(compatibleDays: compatible, acceptedDays: accepted);

        var item = new ConfirmedRideItemViewModel(request, currentUserId);

        Assert.Equal("Terça-feira, Quarta-feira", item.CompatibleDaysText);
        Assert.DoesNotContain("Segunda-feira", item.CompatibleDaysText);
        Assert.Equal(compatible, request.CompatibleDays);
        Assert.Equal(accepted, request.AcceptedDays);
        Assert.False(item.HasRequestedDate);
    }

    [Fact]
    public void WeeklyWithoutAcceptedDays_DoesNotFallBackToRequestedDays()
    {
        var request = CreateRequest(acceptedDays: []);
        var item = new ConfirmedRideItemViewModel(request, request.PassengerUserId);

        Assert.Empty(item.CompatibleDaysText);
    }

    private static RideRequest CreateRequest(
        string passengerUserName = "Ana",
        string driverUserName = "Bruno",
        IReadOnlyList<DayOfWeek>? compatibleDays = null,
        RideRequestType type = RideRequestType.Weekly,
        DateOnly? requestedDate = null,
        decimal suggestedPrice = 3.45m,
        IReadOnlyList<DayOfWeek>? acceptedDays = null)
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
            AcceptedDays = type == RideRequestType.Weekly
                ? acceptedDays ?? compatibleDays ?? [DayOfWeek.Friday]
                : [],
            Type = type,
            Status = RideRequestStatus.Accepted,
            RequestedDate = requestedDate,
            SuggestedPrice = suggestedPrice,
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
