using System.Text.Json.Nodes;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseRideRequestServicePricingTests
{
    [Theory]
    [InlineData(false, "weeklyAcceptedDays", "{\"arrayValue\":{}}")]
    [InlineData(false, "oneTimeAcceptedDates", "{\"arrayValue\":{}}")]
    [InlineData(true, "seatCapacity", "{\"nullValue\":null}")]
    [InlineData(true, "weeklyAvailableSeats", "{\"mapValue\":{}}")]
    [InlineData(true, "oneTimeReservations", "{\"arrayValue\":{}}")]
    [InlineData(true, "estimatedDistanceKm", "{\"doubleValue\":0}")]
    [InlineData(true, "requestRevision", "{\"integerValue\":\"0\"}")]
    public async Task AcceptAsync_RejectsOppositeRoleFieldsWithoutAnyWrite(bool passenger, string field, string value)
    {
        var store = SharedPassengerStore([DayOfWeek.Monday], [DayOfWeek.Monday]);
        store.Fields(passenger ? SharedPassengerKey : "weeklyRoutes/driver-route-B")[field] = JsonNode.Parse(value);
        var before = store.Snapshot();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(store, [], "driver-B").AcceptAsync("request-B"));
        Assert.Contains(field, error.Message);
        Assert.Empty(store.Attempts);
        Assert.Equal(before, store.Snapshot());
    }

    [Theory]
    [InlineData("estimatedDistanceKm", null)]
    [InlineData("requestRevision", null)]
    [InlineData("estimatedDistanceKm", "{\"doubleValue\":0}")]
    [InlineData("requestRevision", "{\"integerValue\":\"-1\"}")]
    [InlineData("weeklyAcceptedDays", "{\"arrayValue\":{}}")]
    [InlineData("oneTimeAcceptedDates", "{\"arrayValue\":{}}")]
    public async Task CreateRequest_RequiresStrictDriverSnapshotBeforeCommit(string field, string? value)
    {
        var driver = JsonNode.Parse(CreateCreationDriverRouteDocumentJson())!;
        if (value is null) driver["fields"]!.AsObject().Remove(field);
        else driver["fields"]![field] = JsonNode.Parse(value);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(JsonResponse(driver.ToJsonString()));
        });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(handler, [CreatePassengerRoute()]).CreateAsync(
                "passenger-route", CreateMatch(), RideRequestType.Weekly, null, 3.45m));
        Assert.Contains(field, error.Message);
        Assert.Equal(1, handler.RequestCount);
    }
}
