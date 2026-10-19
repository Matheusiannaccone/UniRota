using System.Globalization;
using System.Text.Json.Nodes;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseRouteServiceTests
{
    [Theory]
    [InlineData("2026-10-19T12:33:55.240475Z", 1792413235L, 240475000)]
    [InlineData("2026-10-19T12:33:55Z", 1792413235L, 0)]
    public async Task UpdateAsync_EmulatorReceivesExactVersionInsteadOfTimestampZero(
        string version, long seconds, int nanos)
    {
        var stored = CreateSeatDocument();
        stored["updateTime"] = version;
        var patches = 0;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(stored.ToJsonString());
            }

            Assert.Equal(HttpMethod.Patch, request.Method);
            patches++;
            var query = request.RequestUri!.Query;

            // Reproduz o emulador: Timestamp RFC3339 na query gera base version 0.
            if (query.Contains("currentDocument.updateTime=", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":{\"status\":\"FAILED_PRECONDITION\",\"message\":\"required base version (0)\"}}")
                };
            }

            Assert.Contains("currentDocument.updateTime.seconds=" + seconds.ToString(CultureInfo.InvariantCulture), query);
            Assert.Contains("currentDocument.updateTime.nanos=" + nanos.ToString(CultureInfo.InvariantCulture), query);
            var payload = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            foreach (var entry in payload["fields"]!.AsObject())
            {
                stored["fields"]![entry.Key] = entry.Value?.DeepClone();
            }

            return JsonResponse(stored.ToJsonString());
        });

        var result = await CreateService(handler, useEmulators: true)
            .UpdateAsync(CreateSeatSchemaRoute(capacity: 4));

        Assert.Equal(4, result.SeatCapacity);
        Assert.All(result.WeeklyAvailableSeats.Values, value => Assert.Equal(4, value));
        Assert.Equal(1, patches);
    }

    [Fact]
    public async Task UpdateAsync_EmulatorStillRejectsRealVersionConflictWithoutRetry()
    {
        var requests = new List<HttpMethod>();
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            requests.Add(request.Method);
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse(CreateSeatDocument().ToJsonString()));
            }

            Assert.Contains("currentDocument.updateTime.seconds=", request.RequestUri!.Query);
            Assert.Contains("currentDocument.updateTime.nanos=", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":{\"status\":\"FAILED_PRECONDITION\"}}")
            });
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(handler, useEmulators: true).UpdateAsync(CreateSeatSchemaRoute(capacity: 4)));

        Assert.Equal("FAILED_PRECONDITION", exception.Data["FirebaseCode"]);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Patch }, requests);
    }

    [Theory]
    [InlineData(RouteRole.Driver, 4, false, false)]
    [InlineData(RouteRole.Driver, 2, false, false)]
    [InlineData(RouteRole.Driver, 3, true, false)]
    [InlineData(RouteRole.Driver, 3, false, true)]
    [InlineData(RouteRole.Driver, 4, true, true)]
    [InlineData(RouteRole.Passenger, 0, true, false)]
    [InlineData(RouteRole.Passenger, 0, false, true)]
    public async Task UpdateAsync_UnoccupiedRoutePatchContainsNewCapacityAndDays(
        RouteRole role, int capacity, bool addThursday, bool removeWednesday)
    {
        var stored = CreateOccupiedSeatDocument();
        var existingFields = stored["fields"]!.AsObject();
        var passenger = role == RouteRole.Passenger;
        existingFields["role"]!["stringValue"] = passenger ? "passenger" : "driver";
        existingFields["seatCapacity"] = passenger
            ? new JsonObject { ["nullValue"] = null }
            : new JsonObject { ["integerValue"] = "3" };
        var initialWeekly = new JsonObject();

        if (!passenger)
        {
            foreach (var day in new[] { "Monday", "Tuesday", "Wednesday" })
            {
                initialWeekly[day] = new JsonObject { ["integerValue"] = "3" };
            }
        }

        existingFields["weeklyAvailableSeats"]!["mapValue"]!["fields"] = initialWeekly;
        // O emulador omite 'values' quando não há reservas: reproduzir sua resposta real.
        existingFields["oneTimeReservations"]!["arrayValue"] = new JsonObject();
        var selectedDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday };
        if (!removeWednesday) selectedDays.Add(DayOfWeek.Wednesday);
        if (addThursday) selectedDays.Add(DayOfWeek.Thursday);
        var edited = new WeeklyRoute
        {
            Id = "schema-route",
            Role = role,
            Origin = "Nova origem",
            OriginPlaceId = "new-origin-place",
            Destination = "Facens",
            DestinationPlaceId = "destination-place-id",
            DepartureTimeMinutes = 510,
            DaysOfWeek = selectedDays,
            SeatCapacity = passenger ? null : capacity,
            EstimatedDistanceKm = passenger ? 0m : 10m
        };
        JsonObject? patch = null;
        var service = CreateSeatEditingService(stored, (fields, _) => patch = fields);

        var result = await service.UpdateAsync(edited);

        Assert.NotNull(patch);
        Assert.Equal(selectedDays, result.DaysOfWeek);
        Assert.Equal(selectedDays.Select(day => day.ToString()),
            patch["daysOfWeek"]!["arrayValue"]!["values"]!.AsArray()
                .Select(day => day!["stringValue"]!.GetValue<string>()));
        Assert.Equal("Nova origem", result.Origin);
        Assert.Equal(510, result.DepartureTimeMinutes);
        Assert.Empty(result.OneTimeReservations);
        var weekly = patch["weeklyAvailableSeats"]!["mapValue"]!["fields"]!.AsObject();

        if (passenger)
        {
            Assert.Null(result.SeatCapacity);
            Assert.True(patch["seatCapacity"]!.AsObject().ContainsKey("nullValue"));
            Assert.Empty(weekly);
        }
        else
        {
            Assert.Equal(capacity, result.SeatCapacity);
            Assert.Equal(capacity.ToString(CultureInfo.InvariantCulture),
                patch["seatCapacity"]!["integerValue"]!.GetValue<string>());
            Assert.Equal(selectedDays.Count, weekly.Count);
            Assert.All(selectedDays, day => Assert.Equal(capacity, result.WeeklyAvailableSeats[day]));
        }
    }
}
