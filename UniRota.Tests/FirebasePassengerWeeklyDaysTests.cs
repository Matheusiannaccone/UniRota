using System.Globalization;
using System.Text.Json.Nodes;
using UniRota.Models;
using UniRota.Services;

namespace UniRota.Tests;

public sealed partial class FirebaseRideRequestServicePricingTests
{
    [Fact]
    public async Task NonOverlappingDrivers_RetryPreservesPassengerDaysAddedByWinner()
    {
        var store = SharedPassengerStore([DayOfWeek.Tuesday], [DayOfWeek.Monday]);
        var driverA = CreateService(store, [], "driver-A");
        var driverB = CreateService(store, [], "driver-B");
        store.BeforeCommit = async (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1) await driverA.AcceptAsync("request-A");
        };

        await driverB.AcceptAsync("request-B", confirmedDays: [DayOfWeek.Monday]);

        Assert.Equal(new[] { "Tuesday" }, store.AcceptedDays("request-A"));
        Assert.Equal(new[] { "Monday" }, store.AcceptedDays("request-B"));
        Assert.Equal(new[] { "Monday", "Tuesday" },
            store.Fields("weeklyRoutes/passenger-route-request-A")["weeklyAcceptedDays"]!["arrayValue"]!["values"]!.AsArray()
                .Select(value => value!["stringValue"]!.GetValue<string>()));
        Assert.Equal(1, store.Conflicts);
        Assert.Equal(2, store.AttemptsFor("request-B").Length);
        Assert.Equal(1, store.Seats(DayOfWeek.Monday));
        Assert.Equal(0, store.Seats(DayOfWeek.Tuesday));
        store.AssertConsistent();
    }

    [Theory]
    [InlineData(RideRequestType.Weekly)]
    [InlineData(RideRequestType.Once)]
    public async Task PendingForDifferentDriver_DoesNotBlockOverlappingDaysOrReservePassengerLedger(RideRequestType type)
    {
        var existing = JsonNode.Parse(CreateRideRequestDocumentJson("existing", "passenger-user", 3.45m))!;
        existing["fields"]!["driverRouteId"]!["stringValue"] = "another-driver-route";
        existing["fields"]!["driverUserId"]!["stringValue"] = "another-driver";
        var passenger = CreatePassengerRoute();
        string? body = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get) return JsonResponse(CreateCreationDriverRouteDocumentJson());
            if (request.RequestUri!.AbsoluteUri.EndsWith(":runQuery", StringComparison.Ordinal))
                return JsonResponse("[{\"document\":" + existing.ToJsonString() + "}]");
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("{}");
        });

        var pending = await CreateService(handler, [passenger]).CreateAsync(
            passenger.Id, CreateMatch(), type, type == RideRequestType.Once ? TestToday : null, 3.45m);

        Assert.Equal(new[] { DayOfWeek.Monday }, pending.CompatibleDays);
        Assert.Empty(passenger.WeeklyAcceptedDays);
        Assert.Empty(pending.AcceptedDays);
        Assert.Equal(RideRequestStatus.Pending, pending.Status);
        var writes = JsonNode.Parse(body!)!["writes"]!.AsArray();
        Assert.Equal(2, writes.Count);
        Assert.Equal("requestRevision", Assert.Single(writes[1]!["update"]!["fields"]!.AsObject()).Key);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewWeeklyRequest_ExcludesPassengerConfirmedDaysWithoutMutatingOriginalRoute(bool allOccupied)
    {
        var passenger = new WeeklyRoute
        {
            Id = "passenger-route", UserId = "passenger-user", Role = RouteRole.Passenger,
            DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday],
            WeeklyAcceptedDays = allOccupied ? [DayOfWeek.Monday, DayOfWeek.Tuesday] : [DayOfWeek.Monday]
        };
        var handler = new StubHttpMessageHandler((request, _) => Task.FromResult(JsonResponse(
            request.Method == HttpMethod.Get ? CreateCreationDriverRouteDocumentJson(
                new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 2, [DayOfWeek.Tuesday] = 1 })
            : request.RequestUri!.AbsoluteUri.EndsWith(":runQuery", StringComparison.Ordinal) ? "[]" : "{}")));
        var service = CreateService(handler, [passenger]);
        var match = CreateMatch() with { CompatibleDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday } };
        if (allOccupied)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
                passenger.Id, match, RideRequestType.Weekly, null, 3.45m));
            Assert.Equal(RideRequestRules.PassengerDaysUnavailableMessage, exception.Message);
            Assert.Equal(0, handler.RequestCount);
        }
        else
        {
            var created = await service.CreateAsync(passenger.Id, match, RideRequestType.Weekly, null, 3.45m);
            Assert.Equal(new[] { DayOfWeek.Tuesday }, created.CompatibleDays);
            Assert.Empty(created.AcceptedDays);
        }
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }, passenger.DaysOfWeek);
        Assert.Equal(allOccupied ? 2 : 1, passenger.WeeklyAcceptedDays.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentDrivers_CompeteForPassengerDayAndRequireFreshConfirmation(bool partial)
    {
        var store = SharedPassengerStore([DayOfWeek.Monday],
            partial ? [DayOfWeek.Monday, DayOfWeek.Tuesday] : [DayOfWeek.Monday]);
        var driverA = CreateService(store, [], "driver-A");
        var driverB = CreateService(store, [], "driver-B");
        store.BeforeCommit = async (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1) await driverA.AcceptAsync("request-A");
        };
        var originalPassengerDays = store.Fields("weeklyRoutes/passenger-route-request-A")["daysOfWeek"]!.ToJsonString();
        var originalRequestDays = store.Fields("rideRequests/request-B")["compatibleDays"]!.ToJsonString();

        var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() => driverB.AcceptAsync("request-B"));

        Assert.Equal(partial ? RideRequestRules.ConfirmedDaysChangedMessage : RideRequestRules.PassengerDaysUnavailableMessage, conflict.Message);
        Assert.Equal("pending", store.Status("request-B"));
        Assert.Equal("1", store.Fields("weeklyRoutes/driver-route-B")["weeklyAvailableSeats"]!["mapValue"]!["fields"]!["Monday"]!["integerValue"]!.GetValue<string>());
        Assert.Single(store.AttemptsFor("request-B"));
        Assert.False(store.AttemptsFor("request-B")[0].Committed);
        if (partial)
            await driverB.AcceptAsync("request-B", confirmedDays: [DayOfWeek.Tuesday]);

        var ledger = store.Fields("weeklyRoutes/passenger-route-request-A")["weeklyAcceptedDays"]!["arrayValue"]!["values"]!.AsArray();
        Assert.Equal(partial ? ["Monday", "Tuesday"] : new[] { "Monday" }, ledger.Select(value => value!["stringValue"]!.GetValue<string>()));
        Assert.Equal(partial ? new[] { "Tuesday" } : [], store.AcceptedDays("request-B"));
        Assert.Equal(originalPassengerDays, store.Fields("weeklyRoutes/passenger-route-request-A")["daysOfWeek"]!.ToJsonString());
        Assert.Equal(originalRequestDays, store.Fields("rideRequests/request-B")["compatibleDays"]!.ToJsonString());
        Assert.Equal(partial ? 4 : 3, store.DocumentReads["weeklyRoutes/passenger-route-request-A"]);
        Assert.Equal(1, store.Conflicts);
        Assert.All(store.FailedCommits, failed => Assert.Equal(failed.Before, failed.After));
        foreach (var attempt in store.Attempts.Where(attempt => attempt.Committed))
        {
            Assert.Equal(3, attempt.Writes.Count);
            Assert.All(attempt.Writes, write => Assert.NotNull(write!["currentDocument"]!["updateTime"]));
            Assert.Equal("weeklyAcceptedDays", Assert.Single(attempt.Writes[2]!["updateMask"]!["fieldPaths"]!.AsArray())!.GetValue<string>());
        }
        store.AssertConsistent();
    }

    [Theory]
    [InlineData("Wednesday", true)]
    [InlineData("Monday,Wednesday", false)]
    [InlineData("", false)]
    public async Task OldPending_UsesFreshPassengerLedgerAndOnlyConfirmedDays(string confirmation, bool success)
    {
        var store = ConcurrencyStore(RideRequestType.Weekly, RideRequestType.Weekly,
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday]);
        store.EditRoute(fields =>
        {
            fields["daysOfWeek"] = DaysField(["Monday", "Tuesday", "Wednesday"]);
            fields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!["Wednesday"] = new JsonObject { ["integerValue"] = "1" };
        });
        store.Fields("weeklyRoutes/passenger-route-request-B")["weeklyAcceptedDays"] = DaysField(["Monday", "Tuesday"]);
        var service = CreateService(store, [], "driver-user");
        var confirmed = confirmation.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<DayOfWeek>).ToArray();
        var before = store.Snapshot();
        if (success)
        {
            await service.AcceptAsync("request-B", confirmedDays: confirmed);
            Assert.Equal(new[] { "Wednesday" }, store.AcceptedDays("request-B"));
            Assert.Equal(1, store.Seats(DayOfWeek.Monday));
            Assert.Equal(1, store.Seats(DayOfWeek.Tuesday));
            Assert.Equal(0, store.Seats(DayOfWeek.Wednesday));
            Assert.Empty(store.Reservations);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B", confirmedDays: confirmed));
            Assert.Equal(RideRequestRules.ConfirmedDaysChangedMessage, exception.Message);
            Assert.Equal(before, store.Snapshot());
            Assert.Empty(store.Attempts);
        }
    }

    [Fact]
    public async Task Once_IsBlockedByPassengerRecurringLedger()
    {
        var store = ConcurrencyStore(RideRequestType.Once, RideRequestType.Once);
        store.Fields("weeklyRoutes/passenger-route-request-B")["weeklyAcceptedDays"] = DaysField(["Monday"]);
        var before = store.Snapshot();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(store, [], "driver-user").AcceptAsync("request-B"));
        Assert.Equal(RideRequestRules.PassengerWeeklyConflictMessage, error.Message);
        Assert.Equal(before, store.Snapshot());
        Assert.Empty(store.Attempts);
        Assert.True(store.DocumentReads.ContainsKey("weeklyRoutes/passenger-route-request-B"));
    }

    private static StatefulAcceptanceHandler SharedPassengerStore(IReadOnlyList<DayOfWeek> firstDays, IReadOnlyList<DayOfWeek> secondDays,
        RideRequestType firstType = RideRequestType.Weekly, RideRequestType secondType = RideRequestType.Weekly, DateOnly? secondDate = null)
    {
        var store = ConcurrencyStore(firstType, secondType, secondDays, secondDate);
        store.EditRoute(fields => fields["userId"]!["stringValue"] = "driver-A");
        var otherDriver = JsonNode.Parse(CreateCreationDriverRouteDocumentJson(
            new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 1, [DayOfWeek.Tuesday] = 1 }))!.AsObject();
        otherDriver["name"] = "projects/test-project/databases/(default)/documents/weeklyRoutes/driver-route-B";
        otherDriver["fields"]!["userId"]!["stringValue"] = "driver-B";
        store.AddDocument(otherDriver);
        store.EditRequest("request-A", fields => fields["driverUserId"]!["stringValue"] = "driver-A");
        store.EditRequest("request-B", fields =>
        {
            fields["driverUserId"]!["stringValue"] = "driver-B";
            fields["driverRouteId"]!["stringValue"] = "driver-route-B";
            fields["passengerUserId"]!["stringValue"] = "passenger-request-A";
            fields["passengerRouteId"]!["stringValue"] = "passenger-route-request-A";
        });
        store.EditRequest("request-A", fields => fields["compatibleDays"] = DaysField(firstDays.Select(day => day.ToString())));
        return store;
    }

    private static string CreatePassengerRouteDocumentJson(string id = "passenger-route",
        string userId = "passenger-user", IReadOnlyList<DayOfWeek>? days = null,
        IReadOnlyList<DayOfWeek>? accepted = null) => new JsonObject
    {
        ["name"] = "projects/test-project/databases/(default)/documents/weeklyRoutes/" + id,
        ["updateTime"] = "2026-09-03T14:00:00Z",
        ["fields"] = new JsonObject
        {
            ["userId"] = new JsonObject { ["stringValue"] = userId },
            ["role"] = new JsonObject { ["stringValue"] = "passenger" },
            ["daysOfWeek"] = DaysField((days ?? Enum.GetValues<DayOfWeek>()).Select(day => day.ToString())),
            ["weeklyAcceptedDays"] = DaysField((accepted ?? []).Select(day => day.ToString())),
            ["oneTimeAcceptedDates"] = DaysField([]),
            ["seatCapacity"] = new JsonObject { ["nullValue"] = null },
            ["weeklyAvailableSeats"] = new JsonObject { ["mapValue"] = new JsonObject { ["fields"] = new JsonObject() } },
            ["oneTimeReservations"] = new JsonObject { ["arrayValue"] = new JsonObject { ["values"] = new JsonArray() } }
        }
    }.ToJsonString();
}
