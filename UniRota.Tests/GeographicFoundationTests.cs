using UniRota.Models;

namespace UniRota.Tests;

public sealed class GeographicFoundationTests
{
    [Fact]
    public void WeeklyRoute_SupportsOptionalPlaceIds()
    {
        var legacyRoute = new WeeklyRoute();
        var geographicRoute = new WeeklyRoute
        {
            OriginPlaceId = "origin-place-id",
            DestinationPlaceId = "destination-place-id"
        };

        Assert.Equal(string.Empty, legacyRoute.OriginPlaceId);
        Assert.Equal(string.Empty, legacyRoute.DestinationPlaceId);
        Assert.Equal("origin-place-id", geographicRoute.OriginPlaceId);
        Assert.Equal("destination-place-id", geographicRoute.DestinationPlaceId);
    }
}
