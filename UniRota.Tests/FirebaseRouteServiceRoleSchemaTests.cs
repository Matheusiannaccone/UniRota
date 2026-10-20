using System.Text.Json.Nodes;
using UniRota.Models;
using UniRota.Services;
using UniRota.Services.Interfaces;

namespace UniRota.Tests;

public sealed partial class FirebaseRouteServiceTests
{
    private static readonly string[] CommonRouteFields =
    [
        "userId", "userName", "role", "origin", "originPlaceId", "destination",
        "destinationPlaceId", "daysOfWeek", "departureTimeMinutes", "createdAtUtc"
    ];

    private static readonly string[] DriverRouteFields =
        ["seatCapacity", "weeklyAvailableSeats", "oneTimeReservations", "estimatedDistanceKm", "requestRevision"];

    private static readonly string[] PassengerRouteFields = ["weeklyAcceptedDays", "oneTimeAcceptedDates"];

    [Fact]
    public async Task Matching_AcceptsRoutesReadFromBothFinalPersistedSchemas()
    {
        var passengerDocument = CreateSeatDocument(passenger: true);
        var driverDocument = CreateSeatDocument();
        driverDocument["name"] = "projects/test-project/databases/(default)/documents/weeklyRoutes/driver-route";
        driverDocument["fields"]!["userId"]!["stringValue"] = "another-user";
        foreach (var document in new[] { passengerDocument, driverDocument })
        {
            document["fields"]!["originPlaceId"] = new JsonObject { ["stringValue"] = "origin" };
            document["fields"]!["destinationPlaceId"] = new JsonObject { ["stringValue"] = "destination" };
        }
        var passenger = Assert.Single(await ReadSeatDocumentAsync(passengerDocument));
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(
            new JsonArray(new JsonObject { ["document"] = driverDocument }).ToJsonString())));
        var drivers = await CreateService(handler).GetDriverRoutesAsync();
        var matching = new MatchingService(new SchemaMapService(), new MatchingOptions(), new AppClock(TimeProvider.System));

        var match = Assert.Single(await matching.FindMatchesAsync(passenger, drivers));

        Assert.Equal("driver-route", match.DriverRoute.Id);
        Assert.Equal(new[] { DayOfWeek.Monday }, match.CompatibleDays);
    }

    private sealed class SchemaMapService : IMapRouteService
    {
        public Task<MapRouteResult> CalculateAsync(string originPlaceId, string destinationPlaceId,
            IReadOnlyList<string>? intermediatePlaceIds = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MapRouteResult { DistanceMeters = 10000, Duration = TimeSpan.FromMinutes(30), EncodedPolyline = "polyline" });
    }

    [Theory]
    [InlineData(RouteRole.Driver, false)]
    [InlineData(RouteRole.Driver, true)]
    [InlineData(RouteRole.Passenger, false)]
    [InlineData(RouteRole.Passenger, true)]
    public async Task WriteRoute_UsesExactRoleSchemaAndEditableMask(RouteRole role, bool update)
    {
        var input = CreateSeatSchemaRoute(role: role, capacity: role == RouteRole.Driver ? 3 : null);
        var (output, fields, url) = await RoundTripSeatRouteAsync(input, update);
        var expected = update
            ? CommonRouteFields.Except(["userId", "userName", "role", "createdAtUtc"])
                .Concat(role == RouteRole.Driver ? DriverRouteFields.Except(["requestRevision"]) : [])
            : CommonRouteFields.Concat(role == RouteRole.Driver ? DriverRouteFields : PassengerRouteFields);
        Assert.Equal(expected.OrderBy(field => field), fields.Select(field => field.Key).OrderBy(field => field));
        Assert.Equal(role, output.Role);
        if (update)
        {
            var mask = new Uri(url).Query.TrimStart('?').Split('&')
                .Where(part => part.StartsWith("updateMask.fieldPaths=", StringComparison.Ordinal))
                .Select(part => Uri.UnescapeDataString(part.Split('=')[1]));
            Assert.Equal(expected.OrderBy(field => field), mask.OrderBy(field => field));
            Assert.DoesNotContain("weeklyAcceptedDays", url);
            Assert.DoesNotContain("oneTimeAcceptedDates", url);
        }
        else if (role == RouteRole.Passenger)
        {
            Assert.Empty(fields["weeklyAcceptedDays"]!["arrayValue"]!["values"]!.AsArray());
            Assert.Empty(fields["oneTimeAcceptedDates"]!["arrayValue"]!["values"]!.AsArray());
        }
        else Assert.Equal("0", fields["requestRevision"]!["integerValue"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false, "weeklyAcceptedDays", "{\"arrayValue\":{}}")]
    [InlineData(false, "oneTimeAcceptedDates", "{\"arrayValue\":{}}")]
    [InlineData(true, "seatCapacity", "{\"nullValue\":null}")]
    [InlineData(true, "weeklyAvailableSeats", "{\"mapValue\":{}}")]
    [InlineData(true, "oneTimeReservations", "{\"arrayValue\":{}}")]
    [InlineData(true, "estimatedDistanceKm", "{\"doubleValue\":0}")]
    [InlineData(true, "requestRevision", "{\"integerValue\":\"0\"}")]
    public async Task ReadRoute_RejectsOppositeRoleFieldsEvenWhenEmptyOrNull(bool passenger, string field, string value)
    {
        var document = CreateSeatDocument(passenger);
        document["fields"]![field] = JsonNode.Parse(value);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSeatDocumentAsync(document));
        Assert.Contains(field, error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadRoute_RequiresAllCommonFieldsAndOnlyItsRoleFields(bool passenger)
    {
        var document = CreateSeatDocument(passenger);
        var route = Assert.Single(await ReadSeatDocumentAsync(document));
        Assert.Equal(passenger ? RouteRole.Passenger : RouteRole.Driver, route.Role);
        foreach (var field in CommonRouteFields.Concat(passenger ? PassengerRouteFields : DriverRouteFields))
        {
            var invalid = document.DeepClone();
            invalid["fields"]!.AsObject().Remove(field);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSeatDocumentAsync(invalid.AsObject()));
            Assert.Contains(field, error.Message);
        }
    }

    [Theory]
    [InlineData("requestRevision", "{\"integerValue\":\"-1\"}")]
    [InlineData("requestRevision", "{\"stringValue\":\"0\"}")]
    [InlineData("estimatedDistanceKm", "{\"doubleValue\":0}")]
    [InlineData("estimatedDistanceKm", "{\"doubleValue\":-1}")]
    public async Task ReadDriver_RequiresNonnegativeRevisionAndPositiveDistance(string field, string value)
    {
        var document = CreateSeatDocument();
        document["fields"]![field] = JsonNode.Parse(value);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSeatDocumentAsync(document));
        Assert.Contains(field, error.Message);
    }
}
