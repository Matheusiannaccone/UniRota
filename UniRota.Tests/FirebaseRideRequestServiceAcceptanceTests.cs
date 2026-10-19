using System.Net;
using System.Text.Json.Nodes;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseRideRequestServicePricingTests
{
    private static readonly DateOnly TestToday = new(2026, 10, 19);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task AcceptAsync_OnceValidatesDateAgainstInjectedClock(int offset, bool allowed)
    {
        // A solicitação mantém sua data civil; apenas a referência de hoje varia.
        var commits = await RunAcceptanceAsync(RideRequestType.Once, AcceptanceWeeklySeats(), [],
            [DayOfWeek.Monday], allowed: allowed, today: TestToday.AddDays(-offset));

        if (allowed) AssertAcceptanceCommit(Assert.Single(commits), "oneTimeReservations");
    }

    [Fact]
    public async Task AcceptAsync_WeeklyUpdatesOnlyRequestedDaysAndPreservesReservations()
    {
        var weekly = AcceptanceWeeklySeats();
        var reservations = new[] { AcceptanceReservation("existing", TestToday) };

        var commits = await RunAcceptanceAsync(RideRequestType.Weekly, weekly, reservations,
            [DayOfWeek.Monday, DayOfWeek.Tuesday]);

        var fields = AssertAcceptanceCommit(Assert.Single(commits), "weeklyAvailableSeats",
            acceptedDays: [DayOfWeek.Monday, DayOfWeek.Tuesday]);
        var days = fields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!;
        Assert.Equal("1", days["Monday"]!["integerValue"]!.GetValue<string>());
        Assert.Equal("0", days["Tuesday"]!["integerValue"]!.GetValue<string>());
        Assert.Equal("3", days["Wednesday"]!["integerValue"]!.GetValue<string>());
        Assert.Equal(2, weekly[DayOfWeek.Monday]);
        Assert.Single(reservations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptAsync_WeeklySkipsFullOrRemovedDay(bool missing)
    {
        var weekly = AcceptanceWeeklySeats();
        if (missing) weekly.Remove(DayOfWeek.Tuesday);
        else weekly[DayOfWeek.Tuesday] = 0;

        var commits = await RunAcceptanceAsync(RideRequestType.Weekly, weekly, [],
            [DayOfWeek.Monday, DayOfWeek.Tuesday], allowed: true);
        var fields = AssertAcceptanceCommit(Assert.Single(commits), "weeklyAvailableSeats");
        Assert.Equal("1", fields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!["Monday"]!["integerValue"]!.GetValue<string>());
        Assert.Equal(2, weekly[DayOfWeek.Monday]);
    }

    [Theory]
    [InlineData(-7, false)]
    [InlineData(0, true)]
    [InlineData(7, true)]
    public async Task AcceptAsync_WeeklySkipsOnlyWeekdayBlockedByCurrentOrFutureOnce(int offset, bool partial)
    {
        var date = TestToday.AddDays(offset);
        var commits = await RunAcceptanceAsync(RideRequestType.Weekly, AcceptanceWeeklySeats(),
            [AcceptanceReservation("a", date), AcceptanceReservation("b", date)],
            [DayOfWeek.Monday, DayOfWeek.Tuesday]);

        var fields = AssertAcceptanceCommit(Assert.Single(commits), "weeklyAvailableSeats",
            acceptedDays: partial ? [DayOfWeek.Tuesday] : [DayOfWeek.Monday, DayOfWeek.Tuesday]);
        var days = fields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!;
        Assert.Equal(partial ? "2" : "1", days["Monday"]!["integerValue"]!.GetValue<string>());
        Assert.Equal("0", days["Tuesday"]!["integerValue"]!.GetValue<string>());
        Assert.Equal("3", days["Wednesday"]!["integerValue"]!.GetValue<string>());
    }

    [Fact]
    public async Task AcceptAsync_WeeklyRebuildsAcceptedDaysAfterConflict()
    {
        var weekly = AcceptanceWeeklySeats();
        weekly[DayOfWeek.Monday] = 1;
        var commits = await RunAcceptanceAsync(RideRequestType.Weekly, weekly, [],
            [DayOfWeek.Monday, DayOfWeek.Tuesday], conflict: true, reconfirm: true);

        Assert.Equal(2, commits.Count);
        AssertAcceptanceCommit(commits[0], "weeklyAvailableSeats",
            acceptedDays: [DayOfWeek.Monday, DayOfWeek.Tuesday]);
        var fields = AssertAcceptanceCommit(commits[1], "weeklyAvailableSeats", retry: true,
            acceptedDays: [DayOfWeek.Tuesday]);
        Assert.Equal("0", fields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!["Monday"]!["integerValue"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(-7, true)]
    [InlineData(0, false)]
    [InlineData(7, false)]
    public async Task AcceptAsync_WeeklyIgnoresPastOnceAndProtectsTodayAndFuture(int offset, bool allowed)
    {
        var date = TestToday.AddDays(offset);
        var commits = await RunAcceptanceAsync(RideRequestType.Weekly, AcceptanceWeeklySeats(),
            [AcceptanceReservation("a", date), AcceptanceReservation("b", date)],
            [DayOfWeek.Monday], allowed: allowed);
        if (allowed) AssertAcceptanceCommit(Assert.Single(commits), "weeklyAvailableSeats");
    }

    [Fact]
    public async Task AcceptAsync_WeeklyCountsReservationsPerDateAndOnlyChangedWeekdays()
    {
        var commits = await RunAcceptanceAsync(RideRequestType.Weekly, AcceptanceWeeklySeats(),
            [AcceptanceReservation("a", TestToday),
             AcceptanceReservation("b", TestToday.AddDays(7)),
             AcceptanceReservation("c", TestToday.AddDays(1))], [DayOfWeek.Monday]);

        AssertAcceptanceCommit(Assert.Single(commits), "weeklyAvailableSeats");
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, true)]
    [InlineData(2, 0, false)]
    [InlineData(0, 2, true)]
    [InlineData(1, 2, true)]
    public async Task AcceptAsync_OnceUsesExactDateAndPreservesWeeklySeats(
        int sameDateCount, int otherDateCount, bool allowed)
    {
        var weekly = AcceptanceWeeklySeats();
        var reservations = Enumerable.Range(0, sameDateCount + otherDateCount)
            .Select(index => AcceptanceReservation("existing-" + index,
                index < sameDateCount ? TestToday : TestToday.AddDays(7))).ToArray();
        var commits = await RunAcceptanceAsync(RideRequestType.Once, weekly, reservations,
            [DayOfWeek.Monday, DayOfWeek.Tuesday], allowed: allowed);
        Assert.Equal(2, weekly[DayOfWeek.Monday]);
        if (!allowed) return;

        var fields = AssertAcceptanceCommit(Assert.Single(commits), "oneTimeReservations");
        var values = fields["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray();
        Assert.Equal(reservations.Length + 1, values.Count);
        var added = values.Last()!["mapValue"]!["fields"]!.AsObject();
        Assert.Equal(3, added.Count);
        Assert.Equal("request-1", added["requestId"]!["stringValue"]!.GetValue<string>());
        Assert.Equal("2026-10-19", added["date"]!["stringValue"]!.GetValue<string>());
        Assert.Equal("Monday", added["dayOfWeek"]!["stringValue"]!.GetValue<string>());
        for (var index = 0; index < reservations.Length; index++)
        {
            Assert.Equal(reservations[index].RequestId,
                values[index]!["mapValue"]!["fields"]!["requestId"]!["stringValue"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task AcceptAsync_OnceRejectsExistingRequestIdEvenOnAnotherDate()
    {
        await RunAcceptanceAsync(RideRequestType.Once, AcceptanceWeeklySeats(),
            [AcceptanceReservation("request-1", TestToday.AddDays(7))],
            [DayOfWeek.Monday], allowed: false);
    }

    [Theory]
    [InlineData(RideRequestType.Weekly)]
    [InlineData(RideRequestType.Once)]
    public async Task AcceptAsync_ConflictRereadsBothDocumentsAndRetries(RideRequestType type)
    {
        var commits = await RunAcceptanceAsync(type, AcceptanceWeeklySeats(), [],
            [DayOfWeek.Monday], conflict: true);
        Assert.Equal(2, commits.Count);
        var first = AssertAcceptanceCommit(commits[0],
            type == RideRequestType.Weekly ? "weeklyAvailableSeats" : "oneTimeReservations");
        var second = AssertAcceptanceCommit(commits[1],
            type == RideRequestType.Weekly ? "weeklyAvailableSeats" : "oneTimeReservations",
            retry: true);
        if (type == RideRequestType.Weekly)
        {
            Assert.Equal("1", first["weeklyAvailableSeats"]!["mapValue"]!["fields"]!["Monday"]!["integerValue"]!.GetValue<string>());
            Assert.Equal("0", second["weeklyAvailableSeats"]!["mapValue"]!["fields"]!["Monday"]!["integerValue"]!.GetValue<string>());
        }
        else
        {
            Assert.Single(first["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray());
            var values = second["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray();
            Assert.Equal(2, values.Count);
            Assert.Single(values.Where(value => value!["mapValue"]!["fields"]!["requestId"]!["stringValue"]!.GetValue<string>() == "request-1"));
        }
    }

    [Fact]
    public async Task AcceptAsync_RetryRejectsAlreadyReservedRequestWithoutSecondCommit()
    {
        var commits = await RunAcceptanceAsync(RideRequestType.Once, AcceptanceWeeklySeats(), [],
            [DayOfWeek.Monday], allowed: false, conflict: true, existingAfterConflict: true);
        Assert.Single(commits);
    }

    [Fact]
    public async Task AcceptAsync_RetryRevalidatesWeeklyBalanceBeforeSecondCommit()
    {
        var weekly = AcceptanceWeeklySeats();
        weekly[DayOfWeek.Monday] = 1;
        var commits = await RunAcceptanceAsync(RideRequestType.Weekly, weekly, [],
            [DayOfWeek.Monday], allowed: false, conflict: true);
        Assert.Single(commits);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("driverUserId")]
    [InlineData("role")]
    [InlineData("seatCapacity")]
    public async Task AcceptAsync_RejectsProcessedRequestWrongDriverOrInvalidSchema(string invalidField)
    {
        await RunAcceptanceAsync(RideRequestType.Weekly, AcceptanceWeeklySeats(), [],
            [DayOfWeek.Monday], allowed: false, invalidField: invalidField);
    }

    private static Dictionary<DayOfWeek, int> AcceptanceWeeklySeats() => new()
    {
        [DayOfWeek.Monday] = 2, [DayOfWeek.Tuesday] = 1, [DayOfWeek.Wednesday] = 3
    };

    private static OneTimeReservation AcceptanceReservation(string id, DateOnly date) => new()
    {
        RequestId = id, Date = date, DayOfWeek = date.DayOfWeek
    };

    private static JsonObject AssertAcceptanceCommit(string body, string field, bool retry = false,
        IReadOnlyList<DayOfWeek>? acceptedDays = null)
    {
        var writes = JsonNode.Parse(body)!["writes"]!.AsArray();
        Assert.Equal(field == "weeklyAvailableSeats" ? 3 : 2, writes.Count);
        Assert.Equal("accepted", writes[0]!["update"]!["fields"]!["status"]!["stringValue"]!.GetValue<string>());
        Assert.Equal(2, writes[0]!["update"]!["fields"]!.AsObject().Count);
        var expectedDays = acceptedDays ?? (field == "weeklyAvailableSeats" ? new[] { DayOfWeek.Monday } : []);
        Assert.Equal(expectedDays.Select(day => day.ToString()), writes[0]!["update"]!["fields"]!
            ["acceptedDays"]!["arrayValue"]!["values"]!.AsArray().Select(value => value!["stringValue"]!.GetValue<string>()));
        Assert.Equal("projects/test-project/databases/(default)/documents/rideRequests/request-1",
            writes[0]!["update"]!["name"]!.GetValue<string>());
        Assert.Equal("projects/test-project/databases/(default)/documents/weeklyRoutes/driver-route",
            writes[1]!["update"]!["name"]!.GetValue<string>());
        Assert.Equal(new[] { "status", "acceptedDays" }, writes[0]!["updateMask"]!["fieldPaths"]!.AsArray()
            .Select(value => value!.GetValue<string>()));
        Assert.Equal(field, Assert.Single(writes[1]!["updateMask"]!["fieldPaths"]!.AsArray())!.GetValue<string>());
        Assert.Equal(retry ? "2026-09-03T13:00:01Z" : "2026-09-03T13:00:00Z",
            writes[0]!["currentDocument"]!["updateTime"]!.GetValue<string>());
        Assert.Equal(retry ? "2026-09-03T12:00:01Z" : "2026-09-03T12:00:00Z",
            writes[1]!["currentDocument"]!["updateTime"]!.GetValue<string>());
        if (field == "weeklyAvailableSeats")
        {
            Assert.Equal("2026-09-03T14:00:00Z", writes[2]!["currentDocument"]!["updateTime"]!.GetValue<string>());
            Assert.Equal("weeklyAcceptedDays", Assert.Single(writes[2]!["updateMask"]!["fieldPaths"]!.AsArray())!.GetValue<string>());
            Assert.Equal(expectedDays.Select(day => day.ToString()), writes[2]!["update"]!["fields"]!["weeklyAcceptedDays"]!["arrayValue"]!["values"]!.AsArray().Select(value => value!["stringValue"]!.GetValue<string>()));
        }
        var fields = writes[1]!["update"]!["fields"]!.AsObject();
        Assert.Equal(field, Assert.Single(fields).Key);
        return fields;
    }

    private static async Task<List<string>> RunAcceptanceAsync(
        RideRequestType type, IReadOnlyDictionary<DayOfWeek, int> weekly,
        IReadOnlyList<OneTimeReservation> reservations, IReadOnlyList<DayOfWeek> days,
        bool allowed = true, bool conflict = false, bool existingAfterConflict = false,
        string? invalidField = null, DateOnly? today = null, bool reconfirm = false)
    {
        var requestReads = 0;
        var routeReads = 0;
        var commits = new List<string>();
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            var retry = commits.Count > 0;
            if (request.Method == HttpMethod.Get && url.Contains("/rideRequests/request-1", StringComparison.Ordinal))
            {
                requestReads++;
                var document = JsonNode.Parse(CreateRideRequestDocumentJson("request-1", "passenger-user", 3.45m))!;
                var fields = document["fields"]!;
                fields["type"] = new JsonObject { ["stringValue"] = type == RideRequestType.Once ? "once" : "weekly" };
                fields["compatibleDays"] = new JsonObject { ["arrayValue"] = new JsonObject
                {
                    ["values"] = new JsonArray(days.Select(day => (JsonNode)new JsonObject { ["stringValue"] = day.ToString() }).ToArray())
                } };
                if (type == RideRequestType.Once)
                    fields["requestedDate"] = new JsonObject { ["stringValue"] = "2026-10-19" };
                if (invalidField == "status")
                {
                    fields["status"]!["stringValue"] = "accepted";
                    fields["acceptedDays"] = new JsonObject { ["arrayValue"] = new JsonObject
                    {
                        ["values"] = new JsonArray(new JsonObject { ["stringValue"] = days[0].ToString() })
                    } };
                }
                if (invalidField == "driverUserId") fields["driverUserId"]!["stringValue"] = "other-driver";
                if (retry) document["updateTime"] = "2026-09-03T13:00:01Z";
                return JsonResponse(document.ToJsonString());
            }

            if (request.Method == HttpMethod.Get && url.Contains("/weeklyRoutes/passenger-route", StringComparison.Ordinal))
                return JsonResponse(CreatePassengerRouteDocumentJson(days: days));
            if (request.Method == HttpMethod.Get && url.Contains("/weeklyRoutes/driver-route", StringComparison.Ordinal))
            {
                routeReads++;
                var currentWeekly = weekly.ToDictionary(entry => entry.Key, entry => entry.Value);
                var currentReservations = reservations.ToList();
                if (retry)
                {
                    if (type == RideRequestType.Weekly) currentWeekly[DayOfWeek.Monday]--;
                    else currentReservations.Add(AcceptanceReservation(existingAfterConflict ? "request-1" : "concurrent", TestToday));
                }
                var document = JsonNode.Parse(CreateCreationDriverRouteDocumentJson(currentWeekly, currentReservations))!;
                if (retry) document["updateTime"] = "2026-09-03T12:00:01Z";
                if (invalidField == "role") document["fields"]!["role"]!["stringValue"] = "passenger";
                if (invalidField == "seatCapacity") document["fields"]!.AsObject().Remove("seatCapacity");
                return JsonResponse(document.ToJsonString());
            }

            if (url.EndsWith(":commit", StringComparison.Ordinal))
            {
                commits.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                if (conflict && commits.Count == 1)
                {
                    var response = JsonResponse("{\"error\":{\"status\":\"FAILED_PRECONDITION\",\"message\":\"conflict\"}}");
                    response.StatusCode = HttpStatusCode.Conflict;
                    return response;
                }
                return JsonResponse("{}");
            }

            // Aceite não deve consultar nem escrever outras Pending.
            throw new InvalidOperationException("Requisição inesperada: " + url);
        });
        var service = CreateService(handler, [], "driver-user", today);
        if (reconfirm)
        {
            var changed = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-1"));
            Assert.Equal(UniRota.Services.RideRequestRules.ConfirmedDaysChangedMessage, changed.Message);
            await service.AcceptAsync("request-1", confirmedDays: [DayOfWeek.Tuesday]);
        }
        else if (allowed) await service.AcceptAsync("request-1", confirmedDays: type == RideRequestType.Weekly
            ? UniRota.Services.RideRequestRules.GetAcceptableWeeklyDays(weekly, reservations, days, today ?? TestToday) : null);
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-1", confirmedDays: type == RideRequestType.Weekly
                ? UniRota.Services.RideRequestRules.GetAcceptableWeeklyDays(weekly, reservations, days, today ?? TestToday) : null));
            Assert.Equal(conflict ? 1 : 0, commits.Count);
        }
        Assert.Equal(reconfirm ? 3 : conflict ? 2 : 1, requestReads);
        Assert.Equal(invalidField is "status" or "driverUserId" ? 0 : reconfirm ? 3 : conflict ? 2 : 1, routeReads);
        return commits;
    }
}
