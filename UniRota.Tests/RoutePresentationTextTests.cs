using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class RoutePresentationTextTests
{
    [Fact]
    public void OriginDestinationText_SummarizesCompleteGoogleAddresses()
    {
        const string origin =
            "Av. Dr. Eugênio Salerno, 140 - Centro, Sorocaba - SP, "
            + "18035-430, Brasil";
        const string destination =
            "Rod. Senador José Ermírio de Moraes, 1425 - "
            + "Jardim Constantino Matucci, Sorocaba - SP, 18087-125, Brasil";
        var route = CreateRoute(origin, destination);

        var item = new WeeklyRouteItemViewModel(route);

        Assert.Equal(
            "Av. Dr. Eugênio Salerno, 140 → "
            + "Rod. Senador José Ermírio de Moraes, 1425",
            item.OriginDestinationText);
        Assert.Equal(origin, route.Origin);
        Assert.Equal(destination, route.Destination);
    }

    [Theory]
    [InlineData("Facens", "Facens")]
    [InlineData(
        "Rua da Penha, 123, Centro, Sorocaba, SP, Brasil",
        "Rua da Penha, 123")]
    [InlineData("", "")]
    [InlineData(
        "Facens, Sorocaba, SP, Brasil",
        "Facens, Sorocaba, SP, Brasil")]
    public void OriginDestinationText_UsesSafeAddressFallbacks(
        string origin,
        string expectedOrigin)
    {
        var item = new WeeklyRouteItemViewModel(
            CreateRoute(origin, "Destino curto"));

        Assert.Equal(
            $"{expectedOrigin} → Destino curto",
            item.OriginDestinationText);
    }

    private static WeeklyRoute CreateRoute(
        string origin,
        string destination)
    {
        return new WeeklyRoute
        {
            Origin = origin,
            Destination = destination
        };
    }
}
