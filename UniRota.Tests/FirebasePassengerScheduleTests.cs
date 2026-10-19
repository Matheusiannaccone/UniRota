using System.Globalization;
using System.Text.Json.Nodes;
using UniRota.Models;
using UniRota.Services;

namespace UniRota.Tests;

public sealed partial class FirebaseRideRequestServicePricingTests
{
    private const string SharedPassengerKey = "weeklyRoutes/passenger-route-request-A";

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("noncanonical")]
    [InlineData("wrong-type")]
    public async Task Acceptance_RejectsMalformedPassengerDateSchemaWithoutWrites(string scenario)
    {
        var store = SharedPassengerStore([DayOfWeek.Monday], [DayOfWeek.Monday], secondType: RideRequestType.Once);
        var fields = store.Fields(SharedPassengerKey);
        if (scenario == "missing") fields.Remove("oneTimeAcceptedDates");
        else fields["oneTimeAcceptedDates"] = scenario == "wrong-type"
            ? new JsonObject { ["stringValue"] = "2026-10-28" }
            : DaysField(scenario == "duplicate" ? ["2026-10-28", "2026-10-28"] : ["28/10/2026"]);
        var before = store.Snapshot();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(store, [], "driver-B").AcceptAsync("request-B"));

        Assert.Equal(before, store.Snapshot());
        Assert.Empty(store.Attempts);
    }

    [Theory]
    [InlineData(RideRequestType.Once, RideRequestType.Once, false, 0)]
    [InlineData(RideRequestType.Once, RideRequestType.Once, false, 7)]
    [InlineData(RideRequestType.Weekly, RideRequestType.Once, false, 0)]
    [InlineData(RideRequestType.Once, RideRequestType.Weekly, false, 0)]
    [InlineData(RideRequestType.Once, RideRequestType.Weekly, true, 0)]
    public async Task PassengerScheduleRace_OnlyCompatibleConfirmationsCommit(
        RideRequestType winnerType, RideRequestType loserType, bool partial, int dateOffset)
    {
        var originalDays = partial ? new[] { DayOfWeek.Monday, DayOfWeek.Tuesday } : [DayOfWeek.Monday];
        var store = SharedPassengerStore([DayOfWeek.Monday], originalDays,
            winnerType, loserType, TestToday.AddDays(dateOffset));
        var driverA = CreateService(store, [], "driver-A");
        var driverB = CreateService(store, [], "driver-B");
        var originalPassengerDays = store.Fields(SharedPassengerKey)["daysOfWeek"]!.ToJsonString();
        var originalCompatibleDays = store.Fields("rideRequests/request-B")["compatibleDays"]!.ToJsonString();
        store.BeforeCommit = async (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1) await driverA.AcceptAsync("request-A");
        };

        if (dateOffset == 7)
            await driverB.AcceptAsync("request-B");
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => driverB.AcceptAsync("request-B"));
            Assert.Equal(winnerType == RideRequestType.Weekly ? RideRequestRules.PassengerWeeklyConflictMessage
                : loserType == RideRequestType.Once ? RideRequestRules.PassengerOnceConflictMessage
                : partial ? RideRequestRules.ConfirmedDaysChangedMessage : RideRequestRules.PassengerDaysUnavailableMessage,
                error.Message);
            Assert.Equal("pending", store.Status("request-B"));
            Assert.Single(store.AttemptsFor("request-B"));
            Assert.False(store.AttemptsFor("request-B")[0].Committed);
            Assert.Equal("1", store.Fields("weeklyRoutes/driver-route-B")["weeklyAvailableSeats"]!["mapValue"]!["fields"]!["Monday"]!["integerValue"]!.GetValue<string>());
            Assert.Empty(store.Fields("weeklyRoutes/driver-route-B")["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray());
            if (partial) await driverB.AcceptAsync("request-B", confirmedDays: [DayOfWeek.Tuesday]);
        }

        Assert.Equal(1, store.Conflicts);
        Assert.Equal(partial ? 3 : 2, store.RequestReads["request-B"]);
        Assert.Equal(partial ? 4 : 3, store.DocumentReads[SharedPassengerKey]);
        Assert.Equal(dateOffset == 7 || partial ? "accepted" : "pending", store.Status("request-B"));
        Assert.Equal(originalPassengerDays, store.Fields(SharedPassengerKey)["daysOfWeek"]!.ToJsonString());
        Assert.Equal(originalCompatibleDays, store.Fields("rideRequests/request-B")["compatibleDays"]!.ToJsonString());
        Assert.Equal(winnerType == RideRequestType.Weekly ? new[] { "Monday" }
            : partial ? ["Tuesday"] : [], ReadSchedule(store, "weeklyAcceptedDays"));
        Assert.Equal(winnerType == RideRequestType.Once
            ? dateOffset == 7 ? ["2026-10-19", "2026-10-26"] : new[] { "2026-10-19" } : [],
            ReadSchedule(store, "oneTimeAcceptedDates"));
        Assert.All(store.Attempts, attempt =>
        {
            Assert.Equal(3, attempt.Writes.Count);
            Assert.All(attempt.Writes, write => Assert.NotNull(write!["currentDocument"]!["updateTime"]));
            Assert.Contains("passenger-route-request-A", attempt.Writes[2]!["update"]!["name"]!.GetValue<string>());
            var passengerField = Assert.Single(attempt.Writes[2]!["update"]!["fields"]!.AsObject()).Key;
            Assert.Equal(attempt.RequestId == "request-A"
                    ? winnerType == RideRequestType.Once ? "oneTimeAcceptedDates" : "weeklyAcceptedDays"
                    : loserType == RideRequestType.Once ? "oneTimeAcceptedDates" : "weeklyAcceptedDays", passengerField);
        });
        store.AssertConsistent();
    }

    [Theory]
    [InlineData(RideRequestType.Once)]
    [InlineData(RideRequestType.Weekly)]
    public async Task UnrelatedPassengerEdit_RetryRereadsAllThreeAndPreservesHistory(RideRequestType type)
    {
        var store = SharedPassengerStore([DayOfWeek.Monday], [DayOfWeek.Monday], secondType: type);
        store.BeforeCommit = (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1)
                store.Edit(SharedPassengerKey, fields => fields["oneTimeAcceptedDates"] = DaysField(["2020-01-01"]));
            return Task.CompletedTask;
        };

        await CreateService(store, [], "driver-B").AcceptAsync("request-B");

        Assert.Equal(1, store.Conflicts);
        Assert.Equal(2, store.RequestReads["request-B"]);
        Assert.Equal(2, store.DocumentReads["weeklyRoutes/driver-route-B"]);
        Assert.Equal(2, store.DocumentReads[SharedPassengerKey]);
        Assert.Equal(type == RideRequestType.Once ? new[] { "2020-01-01", "2026-10-19" } : ["2020-01-01"],
            ReadSchedule(store, "oneTimeAcceptedDates"));
        Assert.Equal("accepted", store.Status("request-B"));
        Assert.All(store.FailedCommits, failure => Assert.Equal(failure.Before, failure.After));
        var attempts = store.AttemptsFor("request-B");
        Assert.NotEqual(attempts[0].Writes[2]!["currentDocument"]!["updateTime"]!.GetValue<string>(),
            attempts[1].Writes[2]!["currentDocument"]!["updateTime"]!.GetValue<string>());
        store.AssertConsistent();
    }

    [Theory]
    [InlineData(-7, false)]
    [InlineData(0, false)]
    [InlineData(7, false)]
    [InlineData(7, true)]
    public async Task ExistingPassengerOnce_WeeklyUsesFreshDateAndExactConfirmation(int offset, bool partial)
    {
        var days = partial ? new[] { DayOfWeek.Monday, DayOfWeek.Tuesday } : [DayOfWeek.Monday];
        var store = SharedPassengerStore([DayOfWeek.Monday], days);
        var historicalDate = TestToday.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        store.Fields(SharedPassengerKey)["oneTimeAcceptedDates"] = DaysField([historicalDate]);
        var service = CreateService(store, [], "driver-B");
        if (offset >= 0 && !partial)
        {
            var before = store.Snapshot();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));
            Assert.Equal("Os dias dessa solicitação possuem conflito com caronas já confirmadas do passageiro.", error.Message);
            Assert.Equal(before, store.Snapshot());
            Assert.Empty(store.Attempts);
        }
        else
        {
            var expectedDays = partial ? new[] { DayOfWeek.Tuesday } : [DayOfWeek.Monday];
            await service.AcceptAsync("request-B", confirmedDays: expectedDays);
            Assert.Equal(expectedDays.Select(day => day.ToString()), store.AcceptedDays("request-B"));
            Assert.Equal(partial ? "1" : "0", store.Fields("weeklyRoutes/driver-route-B")["weeklyAvailableSeats"]!["mapValue"]!["fields"]!["Monday"]!["integerValue"]!.GetValue<string>());
        }
        Assert.Equal(new[] { historicalDate }, ReadSchedule(store, "oneTimeAcceptedDates"));
        store.AssertConsistent();
    }

    [Theory]
    [InlineData("once", false)]
    [InlineData("weekly", false)]
    [InlineData("other-date", true)]
    public async Task CreateOnce_ValidatesPassengerAgendaWithoutReservingIt(string scenario, bool allowed)
    {
        var passenger = new WeeklyRoute
        {
            Id = "passenger-route", UserId = "passenger-user", Role = RouteRole.Passenger,
            DaysOfWeek = [DayOfWeek.Monday],
            WeeklyAcceptedDays = scenario == "weekly" ? [DayOfWeek.Monday] : [],
            OneTimeAcceptedDates = scenario == "once" ? [TestToday]
                : scenario == "other-date" ? [TestToday.AddDays(7)] : []
        };
        string? commit = null;
        var handler = new StubHttpMessageHandler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get) return JsonResponse(CreateCreationDriverRouteDocumentJson());
            if (request.RequestUri!.AbsoluteUri.EndsWith(":runQuery", StringComparison.Ordinal)) return JsonResponse("[]");
            commit = await request.Content!.ReadAsStringAsync(ct);
            return JsonResponse("{}");
        });
        var service = CreateService(handler, [passenger]);
        if (allowed)
        {
            var result = await service.CreateAsync(passenger.Id, CreateMatch(), RideRequestType.Once, TestToday, 3.45m);
            Assert.Equal(RideRequestStatus.Pending, result.Status);
            Assert.Equal(2, JsonNode.Parse(commit!)!["writes"]!.AsArray().Count);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
                passenger.Id, CreateMatch(), RideRequestType.Once, TestToday, 3.45m));
            Assert.Equal(scenario == "once" ? "O passageiro já possui uma carona confirmada para esta data."
                : "O passageiro já possui uma carona semanal confirmada para este dia.", error.Message);
            Assert.Equal(0, handler.RequestCount);
        }
        Assert.Equal(scenario == "weekly" ? 1 : 0, passenger.WeeklyAcceptedDays.Count);
        Assert.Equal(scenario == "weekly" ? 0 : 1, passenger.OneTimeAcceptedDates.Count);
    }

    private static string[] ReadSchedule(StatefulAcceptanceHandler store, string field) =>
        store.Fields(SharedPassengerKey)[field]!["arrayValue"]!["values"]!.AsArray()
            .Select(value => value!["stringValue"]!.GetValue<string>()).ToArray();
}
