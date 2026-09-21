using UniRota.Models;
using UniRota.Services;
using UniRota.Services.Interfaces;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class RouteMapTests
{
    private const string ExamplePolyline =
        "_p~iF~ps|U_ulLnnqC_mqNvxq`@";

    [Fact]
    public void MapRouteResult_AcceptsEncodedPolyline()
    {
        var result = new MapRouteResult
        {
            DistanceMeters = 1000,
            Duration = TimeSpan.FromMinutes(5),
            EncodedPolyline = ExamplePolyline
        };

        Assert.Equal(ExamplePolyline, result.EncodedPolyline);
    }

    [Fact]
    public void Decoder_TransformsEncodedPolylineIntoCoordinates()
    {
        var points = EncodedPolylineDecoder.Decode(ExamplePolyline);

        Assert.Equal(3, points.Count);
        Assert.Equal(new MapCoordinate(38.5, -120.2), points[0]);
        Assert.Equal(new MapCoordinate(40.7, -120.95), points[1]);
        Assert.Equal(new MapCoordinate(43.252, -126.453), points[2]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Decoder_EmptyInputReturnsNoCoordinates(string? value)
    {
        Assert.Empty(EncodedPolylineDecoder.Decode(value));
    }

    [Theory]
    [InlineData("_")]
    [InlineData("abc!")]
    public void Decoder_InvalidInputFailsSafely(string value)
    {
        Assert.Throws<FormatException>(() =>
            EncodedPolylineDecoder.Decode(value));
    }

    [Fact]
    public void ViewportCalculator_CoversRouteWithPadding()
    {
        var points = new[]
        {
            new MapCoordinate(-23.5, -47.5),
            new MapCoordinate(-23.4, -47.3)
        };

        var viewport = MapViewportCalculator.Calculate(points);

        Assert.Equal(-23.45, viewport.Center.Latitude, 6);
        Assert.Equal(-47.4, viewport.Center.Longitude, 6);
        Assert.True(viewport.LatitudeDegrees > 0.1);
        Assert.True(viewport.LongitudeDegrees > 0.2);
    }

    [Fact]
    public async Task RouteDetails_LoadsSummaryRouteAndDeduplicatedPins()
    {
        var placeService = new FakePlaceService
        {
            Coordinates = new Dictionary<string, MapCoordinate>(
                StringComparer.Ordinal)
            {
                ["shared-origin"] = new(-23.4708, -47.4287),
                ["shared-destination"] = new(-23.5015, -47.4526)
            }
        };
        var viewModel = new RouteDetailsViewModel(placeService);
        var (passengerRoute, match) = CreateContext(
            driverOriginPlaceId: "shared-origin",
            passengerOriginPlaceId: "shared-origin",
            passengerDestinationPlaceId: "shared-destination",
            driverDestinationPlaceId: "shared-destination");

        viewModel.SetRouteContext(passengerRoute, match);
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Distância total: 12,5 km", viewModel.DistanceText);
        Assert.Equal("Duração: 40 min", viewModel.DurationText);
        Assert.Equal("Desvio: +2,5 km · +10 min", viewModel.DetourText);
        Assert.True(viewModel.HasMapData);
        Assert.Equal(3, viewModel.RoutePoints.Count);
        Assert.NotNull(viewModel.Viewport);
        Assert.Equal(2, viewModel.Pins.Count);
        Assert.Contains("Origem do motorista", viewModel.Pins[0].Label);
        Assert.Contains("Origem do passageiro", viewModel.Pins[0].Label);
        Assert.Contains("Destino do passageiro", viewModel.Pins[1].Label);
        Assert.Contains("Destino do motorista", viewModel.Pins[1].Label);
        Assert.Equal(1, placeService.CoordinateCallCount);
        Assert.Equal(
            ["shared-origin", "shared-destination"],
            placeService.LastPlaceIds);
    }

    [Fact]
    public async Task RouteDetails_WithoutPolylineKeepsTextAndDoesNotLoadPins()
    {
        var placeService = new FakePlaceService();
        var viewModel = new RouteDetailsViewModel(placeService);
        var (passengerRoute, match) = CreateContext(encodedPolyline: string.Empty);

        viewModel.SetRouteContext(passengerRoute, match);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasMapData);
        Assert.True(viewModel.HasMapMessage);
        Assert.Contains("Não foi possível exibir", viewModel.MapMessage);
        Assert.Equal("Distância total: 12,5 km", viewModel.DistanceText);
        Assert.Equal(0, placeService.CoordinateCallCount);
    }

    [Fact]
    public void RouteDetails_DoesNotDependOnRoutesOrRideRequests()
    {
        var constructorParameters = typeof(RouteDetailsViewModel)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(IMapRouteService), constructorParameters);
        Assert.DoesNotContain(typeof(IRideRequestService), constructorParameters);
        Assert.Equal([typeof(IPlaceService)], constructorParameters);
    }

    private static (WeeklyRoute PassengerRoute, MatchResult Match) CreateContext(
        string driverOriginPlaceId = "driver-origin",
        string passengerOriginPlaceId = "passenger-origin",
        string passengerDestinationPlaceId = "passenger-destination",
        string driverDestinationPlaceId = "driver-destination",
        string encodedPolyline = ExamplePolyline)
    {
        var passengerRoute = new WeeklyRoute
        {
            Id = "passenger-route",
            UserId = "passenger-user",
            UserName = "Passageiro",
            Role = RouteRole.Passenger,
            Origin = "Origem do passageiro",
            OriginPlaceId = passengerOriginPlaceId,
            Destination = "Destino do passageiro",
            DestinationPlaceId = passengerDestinationPlaceId,
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 480
        };
        var driverRoute = new WeeklyRoute
        {
            Id = "driver-route",
            UserId = "driver-user",
            UserName = "Motorista",
            Role = RouteRole.Driver,
            Origin = "Origem do motorista",
            OriginPlaceId = driverOriginPlaceId,
            Destination = "Destino do motorista",
            DestinationPlaceId = driverDestinationPlaceId,
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 480,
            AvailableSeats = 1,
            EstimatedDistanceKm = 10m
        };
        var match = new MatchResult(
            driverRoute,
            [DayOfWeek.Monday],
            0,
            BaseDistanceKm: 10m,
            BaseDurationMinutes: 30m,
            SharedDistanceKm: 12.5m,
            SharedDurationMinutes: 40m,
            DetourDistanceKm: 2.5m,
            DetourDurationMinutes: 10m,
            SharedEncodedPolyline: encodedPolyline);

        return (passengerRoute, match);
    }

    private sealed class FakePlaceService : IPlaceService
    {
        public IReadOnlyDictionary<string, MapCoordinate> Coordinates { get; init; } =
            new Dictionary<string, MapCoordinate>(StringComparer.Ordinal);

        public int CoordinateCallCount { get; private set; }

        public IReadOnlyList<string> LastPlaceIds { get; private set; } = [];

        public PlaceAutocompleteSession CreateSession() =>
            new(Guid.NewGuid().ToString());

        public Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(
            string query,
            PlaceAutocompleteSession session,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SelectedPlace> GetPlaceAsync(
            string placeId,
            PlaceAutocompleteSession session,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, MapCoordinate>>
            GetCoordinatesAsync(
                IReadOnlyList<string> placeIds,
                CancellationToken cancellationToken = default)
        {
            CoordinateCallCount++;
            LastPlaceIds = placeIds.ToArray();
            return Task.FromResult(Coordinates);
        }
    }
}
