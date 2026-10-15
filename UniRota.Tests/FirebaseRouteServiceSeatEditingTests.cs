using System.Globalization;
using System.Text.Json.Nodes;
using UniRota.Models;
using UniRota.Services.Firebase;

namespace UniRota.Tests;

public sealed partial class FirebaseRouteServiceTests
{
    [Theory]
    [InlineData(3, 2, 1, 2)]
    [InlineData(5, 4, 3, 4)]
    [InlineData(2, 1, 0, 1)]
    public async Task UpdateAsync_UsesReloadedOccupationAndReservationsInsteadOfClientState(
        int capacity, int monday, int tuesday, int wednesday)
    {
        var stored = CreateOccupiedSeatDocument();
        var reservationValues = stored["fields"]!["oneTimeReservations"]!.DeepClone();
        JsonObject? written = null;
        string? patchUrl = null;
        var service = CreateSeatEditingService(stored, (fields, url) =>
        {
            written = fields;
            patchUrl = url;
        });

        // O cliente não envia reservas e usa saldos desatualizados; o serviço deve ignorá-los.
        var result = await service.UpdateAsync(CreateSeatSchemaRoute(capacity: capacity));

        Assert.NotNull(written);
        Assert.Equal(monday, result.WeeklyAvailableSeats[DayOfWeek.Monday]);
        Assert.Equal(tuesday, result.WeeklyAvailableSeats[DayOfWeek.Tuesday]);
        Assert.Equal(wednesday, result.WeeklyAvailableSeats[DayOfWeek.Wednesday]);
        Assert.Equal(capacity, result.SeatCapacity);
        Assert.Equal(7, result.RequestRevision);
        Assert.Equal("historical", Assert.Single(result.OneTimeReservations).RequestId);
        Assert.True(JsonNode.DeepEquals(reservationValues, written["oneTimeReservations"]));
        Assert.Contains("currentDocument.updateTime=2026-09-21T12%3A00%3A00Z", patchUrl);
        Assert.False(written.ContainsKey("requestRevision"));
        Assert.False(written.ContainsKey("availableSeats"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateAsync_RejectsOverbookingBeforePatch(bool onceConflict)
    {
        var stored = CreateOccupiedSeatDocument();

        if (onceConflict)
        {
            var values = stored["fields"]!["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray();
            values.Add(SeatReservationValue("another", "2020-01-06", "Monday"));
        }

        var patches = 0;
        var service = CreateSeatEditingService(stored, (_, _) => patches++);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(
            CreateSeatSchemaRoute(capacity: onceConflict ? 2 : 1)));

        Assert.Equal(0, patches);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateAsync_RejectsBothRoleChangesUsingPersistedRole(bool passenger)
    {
        var stored = CreateSeatDocument(passenger);
        var patches = 0;
        var service = CreateSeatEditingService(stored, (_, _) => patches++);
        var attempted = CreateSeatSchemaRoute(
            role: passenger ? RouteRole.Driver : RouteRole.Passenger,
            capacity: passenger ? 3 : null);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(attempted));

        Assert.Contains("papel", exception.Message);
        Assert.Equal(0, patches);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotRetryFailedVersionPrecondition()
    {
        var requests = new List<HttpMethod>();
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            requests.Add(request.Method);
            return Task.FromResult(request.Method == HttpMethod.Get
                ? JsonResponse(CreateOccupiedSeatDocument().ToJsonString())
                : new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":{\"status\":\"FAILED_PRECONDITION\"}}")
                });
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(handler).UpdateAsync(CreateSeatSchemaRoute(capacity: 3)));

        Assert.Equal("FAILED_PRECONDITION", exception.Data["FirebaseCode"]);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Patch }, requests);
    }

    private static JsonObject CreateOccupiedSeatDocument()
    {
        var stored = CreateSeatDocument();
        var fields = stored["fields"]!.AsObject();
        fields["seatCapacity"]!["integerValue"] = "3";
        fields["requestRevision"]!["integerValue"] = "7";
        fields["daysOfWeek"]!["arrayValue"]!["values"] = new JsonArray(
            new JsonObject { ["stringValue"] = "Monday" },
            new JsonObject { ["stringValue"] = "Tuesday" },
            new JsonObject { ["stringValue"] = "Wednesday" });
        fields["weeklyAvailableSeats"]!["mapValue"]!["fields"] = new JsonObject
        {
            ["Monday"] = new JsonObject { ["integerValue"] = "2" },
            ["Tuesday"] = new JsonObject { ["integerValue"] = "1" },
            ["Wednesday"] = new JsonObject { ["integerValue"] = "2" }
        };
        fields["oneTimeReservations"]!["arrayValue"]!["values"] = new JsonArray(
            SeatReservationValue("historical", "2020-01-06", "Monday"));
        return stored;
    }

    private static FirebaseRouteService CreateSeatEditingService(
        JsonObject stored,
        Action<JsonObject, string> onPatch)
    {
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(stored.ToJsonString());
            }

            Assert.Equal(HttpMethod.Patch, request.Method);
            var payload = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            var fields = payload["fields"]!.AsObject();
            onPatch(fields, request.RequestUri!.AbsoluteUri);

            foreach (var entry in fields)
            {
                stored["fields"]![entry.Key] = entry.Value?.DeepClone();
            }

            return JsonResponse(stored.ToJsonString());
        });
        return CreateService(handler);
    }
}
