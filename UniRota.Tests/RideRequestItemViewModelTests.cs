using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class RideRequestItemViewModelTests
{
    [Fact]
    public void Item_ExposesSuggestedPriceSnapshot()
    {
        var request = CreateRequest(RideRequestType.Once, 7.89m);

        var item = new RideRequestItemViewModel(
            request,
            "Motorista",
            "Motorista não identificado");

        Assert.Equal(7.89m, item.SuggestedPrice);
    }

    [Fact]
    public void Item_FormatsSuggestedPriceUsingPtBrCulture()
    {
        var request = CreateRequest(RideRequestType.Once, 1234.56m);

        var item = new RideRequestItemViewModel(
            request,
            "Motorista",
            "Motorista não identificado");

        Assert.Equal(
            "Preço sugerido: R$ 1.234,56 por viagem",
            item.SuggestedPriceText);
    }

    [Theory]
    [InlineData(RideRequestType.Once)]
    [InlineData(RideRequestType.Weekly)]
    public void Item_AlwaysPresentsSnapshotAsPerTrip(RideRequestType type)
    {
        var request = CreateRequest(type, 3.45m);

        var item = new RideRequestItemViewModel(
            request,
            "Motorista",
            "Motorista não identificado");

        Assert.Equal(
            "Preço sugerido: R$ 3,45 por viagem",
            item.SuggestedPriceText);
    }

    private static RideRequest CreateRequest(
        RideRequestType type,
        decimal suggestedPrice)
    {
        return new RideRequest
        {
            Id = "request-1",
            PassengerRouteId = "passenger-route-1",
            DriverRouteId = "driver-route-1",
            PassengerUserId = "passenger-user-1",
            DriverUserId = "driver-user-1",
            Type = type,
            CompatibleDays = [DayOfWeek.Monday],
            RequestedDate = type == RideRequestType.Once
                ? new DateOnly(2026, 9, 7)
                : null,
            SuggestedPrice = suggestedPrice,
            Status = RideRequestStatus.Pending,
            CreatedAtUtc = new DateTimeOffset(
                2026,
                9,
                3,
                12,
                0,
                0,
                TimeSpan.Zero)
        };
    }
}
