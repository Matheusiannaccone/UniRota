using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseRideRequestServicePricingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentWeekly_LastSeatIsConsumedOnceAndRetryRebuildsGrantedDays(bool partial)
    {
        var store = ConcurrencyStore(RideRequestType.Weekly, RideRequestType.Weekly,
            partial ? [DayOfWeek.Monday, DayOfWeek.Tuesday] : [DayOfWeek.Monday]);
        var service = CreateService(store, [], "driver-user");
        store.BeforeCommit = async (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1) await service.AcceptAsync("request-A");
        };

        if (partial)
        {
            var changed = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));
            Assert.Equal(UniRota.Services.RideRequestRules.ConfirmedDaysChangedMessage, changed.Message);
            Assert.Equal("pending", store.Status("request-B"));
            Assert.Equal(1, store.Seats(DayOfWeek.Tuesday));
            await service.AcceptAsync("request-B", confirmedDays: [DayOfWeek.Tuesday]);
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));

        Assert.Equal("accepted", store.Status("request-A"));
        Assert.Equal(new[] { "Monday" }, store.AcceptedDays("request-A"));
        Assert.Equal(partial ? "accepted" : "pending", store.Status("request-B"));
        Assert.Equal(partial ? ["Tuesday"] : Array.Empty<string>(), store.AcceptedDays("request-B"));
        Assert.Equal(0, store.Seats(DayOfWeek.Monday));
        Assert.Equal(partial ? 0 : 1, store.Seats(DayOfWeek.Tuesday));
        Assert.Equal(1, store.RequestReads["request-A"]);
        Assert.Equal(partial ? 3 : 2, store.RequestReads["request-B"]);
        Assert.Equal(partial ? 4 : 3, store.RouteReads);
        Assert.Equal(1, store.Conflicts);
        if (partial)
        {
            var attempts = store.AttemptsFor("request-B");
            Assert.Equal(new[] { "Monday", "Tuesday" }, WrittenAcceptedDays(attempts[0]));
            Assert.Equal(new[] { "Tuesday" }, WrittenAcceptedDays(attempts[1]));
            Assert.NotEqual(RoutePrecondition(attempts[0]), RoutePrecondition(attempts[1]));
        }
        store.AssertConsistent();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task ConcurrentOnce_CountsExactDateAndPreservesWinningReservation(int dateOffset)
    {
        var store = ConcurrencyStore(RideRequestType.Once, RideRequestType.Once,
            secondDate: TestToday.AddDays(dateOffset));
        var service = CreateService(store, [], "driver-user");
        store.BeforeCommit = async (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1) await service.AcceptAsync("request-A");
        };

        if (dateOffset == 0)
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));
        else await service.AcceptAsync("request-B");

        Assert.Equal("accepted", store.Status("request-A"));
        Assert.Equal(dateOffset == 0 ? "pending" : "accepted", store.Status("request-B"));
        Assert.Equal(dateOffset == 0 ? 1 : 2, store.Reservations.Count);
        Assert.Equal("request-A", store.Reservations[0]!["mapValue"]!["fields"]!["requestId"]!["stringValue"]!.GetValue<string>());
        if (dateOffset != 0)
        {
            Assert.Equal("2026-10-26", store.Reservations[1]!["mapValue"]!["fields"]!["date"]!["stringValue"]!.GetValue<string>());
            var attempts = store.AttemptsFor("request-B");
            Assert.Single(attempts[0].Writes[1]!["update"]!["fields"]!["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray());
            Assert.Equal(2, attempts[1].Writes[1]!["update"]!["fields"]!["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray().Count);
        }
        Assert.Empty(store.AcceptedDays("request-A"));
        Assert.Empty(store.AcceptedDays("request-B"));
        Assert.Equal(1, store.Seats(DayOfWeek.Monday));
        Assert.Equal(2, store.RequestReads["request-B"]);
        Assert.Equal(3, store.RouteReads);
        Assert.Equal(1, store.Conflicts);
        store.AssertConsistent();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentOnceWins_WeeklyRechecksNewReservationAndOnlyGrantsViableDays(bool partial)
    {
        var store = ConcurrencyStore(RideRequestType.Once, RideRequestType.Weekly,
            partial ? [DayOfWeek.Monday, DayOfWeek.Tuesday] : [DayOfWeek.Monday]);
        var service = CreateService(store, [], "driver-user");
        store.BeforeCommit = async (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1) await service.AcceptAsync("request-A");
        };

        if (partial)
        {
            var changed = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));
            Assert.Equal(UniRota.Services.RideRequestRules.ConfirmedDaysChangedMessage, changed.Message);
            Assert.Equal("pending", store.Status("request-B"));
            await service.AcceptAsync("request-B", confirmedDays: [DayOfWeek.Tuesday]);
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));

        Assert.Equal("accepted", store.Status("request-A"));
        Assert.Single(store.Reservations);
        Assert.Equal(partial ? "accepted" : "pending", store.Status("request-B"));
        Assert.Equal(partial ? ["Tuesday"] : Array.Empty<string>(), store.AcceptedDays("request-B"));
        Assert.Equal(1, store.Seats(DayOfWeek.Monday));
        Assert.Equal(partial ? 0 : 1, store.Seats(DayOfWeek.Tuesday));
        Assert.Equal(1, store.Conflicts);
        store.AssertConsistent();
    }

    [Fact]
    public async Task ConcurrentWeeklyWins_OnceUsesNewWeeklyBalanceAndCannotOverbook()
    {
        var store = ConcurrencyStore(RideRequestType.Weekly, RideRequestType.Once);
        var service = CreateService(store, [], "driver-user");
        store.BeforeCommit = async (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1) await service.AcceptAsync("request-A");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));

        Assert.Equal("accepted", store.Status("request-A"));
        Assert.Equal("pending", store.Status("request-B"));
        Assert.Empty(store.Reservations);
        Assert.Equal(0, store.Seats(DayOfWeek.Monday));
        Assert.Equal(2, store.RequestReads["request-B"]);
        Assert.Equal(3, store.RouteReads);
        Assert.Equal(1, store.Conflicts);
        store.AssertConsistent();
    }

    [Theory]
    [InlineData("capacity")]
    [InlineData("weekly-balance")]
    [InlineData("offered-days")]
    [InlineData("reservation")]
    public async Task ConcurrentRouteEdit_InvalidatesOldCommitAndRetryUsesEditedState(string change)
    {
        var store = ConcurrencyStore(RideRequestType.Weekly, RideRequestType.Weekly,
            [DayOfWeek.Monday, DayOfWeek.Tuesday]);
        var service = CreateService(store, [], "driver-user");
        store.BeforeCommit = (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1)
            {
                store.EditRoute(fields =>
                {
                    var weekly = fields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!.AsObject();
                    switch (change)
                    {
                        case "capacity":
                            fields["seatCapacity"]!["integerValue"] = "2";
                            weekly["Monday"]!["integerValue"] = "2";
                            weekly["Tuesday"]!["integerValue"] = "2";
                            break;
                        case "weekly-balance": weekly["Monday"]!["integerValue"] = "0"; break;
                        case "offered-days":
                            fields["daysOfWeek"] = DaysField(["Tuesday"]);
                            weekly.Remove("Monday");
                            break;
                        case "reservation":
                            fields["oneTimeReservations"] = JsonNode.Parse(CreateCreationDriverRouteDocumentJson(
                                reservations: [AcceptanceReservation("external-once", TestToday)]))!["fields"]!["oneTimeReservations"]!.DeepClone();
                            break;
                    }
                    fields["requestRevision"]!["integerValue"] = "7";
                });
            }
            return Task.CompletedTask;
        };

        if (change == "capacity") await service.AcceptAsync("request-B");
        else
        {
            var changed = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));
            Assert.Equal(UniRota.Services.RideRequestRules.ConfirmedDaysChangedMessage, changed.Message);
            Assert.Equal("pending", store.Status("request-B"));
            await service.AcceptAsync("request-B", confirmedDays: [DayOfWeek.Tuesday]);
        }

        var attempts = store.AttemptsFor("request-B");
        Assert.Equal(2, attempts.Length);
        Assert.False(attempts[0].Committed);
        Assert.True(attempts[1].Committed);
        Assert.NotEqual(RoutePrecondition(attempts[0]), RoutePrecondition(attempts[1]));
        Assert.Equal(new[] { "Monday", "Tuesday" }, WrittenAcceptedDays(attempts[0]));
        Assert.Equal(change == "capacity" ? ["Monday", "Tuesday"] : new[] { "Tuesday" }, store.AcceptedDays("request-B"));
        Assert.Equal(change == "capacity" ? 1 : 0, store.Seats(DayOfWeek.Tuesday));
        if (change != "offered-days")
            Assert.Equal(change is "capacity" or "reservation" ? 1 : 0, store.Seats(DayOfWeek.Monday));
        else Assert.False(store.RouteFields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!.AsObject().ContainsKey("Monday"));
        Assert.Equal(change == "capacity" ? "2" : "1", store.RouteFields["seatCapacity"]!["integerValue"]!.GetValue<string>());
        Assert.Equal("7", store.RouteFields["requestRevision"]!["integerValue"]!.GetValue<string>());
        Assert.Equal(change == "reservation" ? 1 : 0, store.Reservations.Count);
        Assert.Equal(change == "capacity" ? 2 : 3, store.RequestReads["request-B"]);
        Assert.Equal(change == "capacity" ? 2 : 3, store.RouteReads);
        store.AssertConsistent();
    }

    [Fact]
    public async Task ConcurrentRequestRejection_InvalidatesWholeCommitAndRetryRereadsStatus()
    {
        var store = ConcurrencyStore(RideRequestType.Weekly, RideRequestType.Weekly);
        var service = CreateService(store, [], "driver-user");
        store.BeforeCommit = (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1)
                store.EditRequest(id, fields => fields["status"]!["stringValue"] = "rejected");
            return Task.CompletedTask;
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));

        Assert.Equal("Esta solicitação já foi processada.", exception.Message);
        Assert.Equal("rejected", store.Status("request-B"));
        Assert.Empty(store.AcceptedDays("request-B"));
        Assert.Equal(1, store.Seats(DayOfWeek.Monday));
        Assert.Single(store.Attempts);
        Assert.Equal(1, store.Conflicts);
        Assert.Equal(2, store.RequestReads["request-B"]);
        Assert.Equal(1, store.RouteReads);
        store.AssertConsistent();
    }

    [Theory]
    [InlineData(RideRequestType.Weekly)]
    [InlineData(RideRequestType.Once)]
    public async Task ConcurrentAndRepeatedAcceptsOfSameRequestConsumeOnlyOnce(RideRequestType type)
    {
        var store = ConcurrencyStore(type, type);
        var service = CreateService(store, [], "driver-user");
        store.BeforeCommit = async (id, attempt) =>
        {
            if (id == "request-B" && attempt == 1) await service.AcceptAsync("request-B");
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));
        Assert.Equal("Esta solicitação já foi processada.", exception.Message);
        var saved = store.Snapshot();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync("request-B"));

        Assert.Equal(saved, store.Snapshot());
        Assert.Equal("accepted", store.Status("request-B"));
        Assert.Single(store.Attempts.Where(attempt => attempt.Committed));
        Assert.Equal(2, store.Attempts.Count);
        Assert.Equal(type == RideRequestType.Once ? 1 : 0, store.Reservations.Count);
        Assert.Equal(type == RideRequestType.Weekly ? 0 : 1, store.Seats(DayOfWeek.Monday));
        Assert.Equal(type == RideRequestType.Weekly ? new[] { "Monday" } : [], store.AcceptedDays("request-B"));
        store.AssertConsistent();
    }

    [Fact]
    public async Task RejectedRequestCannotStartAnotherAcceptance()
    {
        var store = ConcurrencyStore(RideRequestType.Weekly, RideRequestType.Weekly);
        store.EditRequest("request-B", fields => fields["status"]!["stringValue"] = "rejected");
        var saved = store.Snapshot();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(store, [], "driver-user").AcceptAsync("request-B"));

        Assert.Equal("Esta solicitação já foi processada.", exception.Message);
        Assert.Equal(saved, store.Snapshot());
        Assert.Empty(store.Attempts);
        Assert.Equal(0, store.RouteReads);
    }

    [Theory]
    [InlineData(RideRequestType.Weekly, "ABORTED")]
    [InlineData(RideRequestType.Once, "FAILED_PRECONDITION")]
    public async Task EveryAttemptConflicts_StopsAtCurrentLimitWithoutPartialConsumption(
        RideRequestType type, string conflictStatus)
    {
        var store = ConcurrencyStore(type, type);
        store.ConflictStatus = conflictStatus;
        store.BeforeCommit = (_, attempt) =>
        {
            store.EditRoute(fields => fields["requestRevision"]!["integerValue"] = attempt.ToString(CultureInfo.InvariantCulture));
            return Task.CompletedTask;
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(store, [], "driver-user").AcceptAsync("request-B"));

        Assert.Equal("A solicitação foi alterada por outra operação. Recarregue a lista e tente novamente.", exception.Message);
        Assert.Equal(3, store.Attempts.Count);
        Assert.Equal(3, store.Conflicts);
        Assert.Equal(3, store.RequestReads["request-B"]);
        Assert.Equal(3, store.RouteReads);
        Assert.Equal(3, store.Attempts.Select(RoutePrecondition).Distinct().Count());
        Assert.All(store.Attempts, attempt => Assert.False(attempt.Committed));
        Assert.Equal("pending", store.Status("request-B"));
        Assert.Empty(store.AcceptedDays("request-B"));
        Assert.Equal(1, store.Seats(DayOfWeek.Monday));
        Assert.Empty(store.Reservations);
        store.AssertConsistent();
    }

    private static string[] WrittenAcceptedDays(CommitAttempt attempt) => attempt.Writes[0]!["update"]!["fields"]!
        ["acceptedDays"]!["arrayValue"]!["values"]!.AsArray().Select(value => value!["stringValue"]!.GetValue<string>()).ToArray();

    private static string RoutePrecondition(CommitAttempt attempt) =>
        attempt.Writes[1]!["currentDocument"]!["updateTime"]!.GetValue<string>();

    private static StatefulAcceptanceHandler ConcurrencyStore(RideRequestType firstType,
        RideRequestType secondType, IReadOnlyList<DayOfWeek>? secondDays = null, DateOnly? secondDate = null)
    {
        var route = JsonNode.Parse(CreateCreationDriverRouteDocumentJson(
            new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 1, [DayOfWeek.Tuesday] = 1 }))!.AsObject();
        route["fields"]!["seatCapacity"]!["integerValue"] = "1";
        return new StatefulAcceptanceHandler(route,
            ConcurrentRequest("request-A", firstType, [DayOfWeek.Monday], TestToday),
            ConcurrentRequest("request-B", secondType, secondDays ?? [DayOfWeek.Monday], secondDate ?? TestToday));
    }

    private static JsonObject ConcurrentRequest(string id, RideRequestType type,
        IReadOnlyList<DayOfWeek> days, DateOnly date)
    {
        var document = JsonNode.Parse(CreateRideRequestDocumentJson(id, "passenger-" + id, 3.45m))!.AsObject();
        var fields = document["fields"]!;
        fields["passengerRouteId"]!["stringValue"] = "passenger-route-" + id;
        fields["type"]!["stringValue"] = type == RideRequestType.Weekly ? "weekly" : "once";
        fields["compatibleDays"] = DaysField(days.Select(day => day.ToString()));
        if (type == RideRequestType.Once)
            fields["requestedDate"] = new JsonObject { ["stringValue"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        return document;
    }

    private sealed record CommitAttempt(string RequestId, JsonArray Writes)
    {
        public bool Committed { get; set; }
    }

    // Simula somente GET e commit REST: valida todas as versões antes de publicar qualquer escrita.
    // O hook intercala outra operação real do serviço após as leituras e antes do commit concorrente.
    private sealed class StatefulAcceptanceHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, JsonObject> _documents;
        private int _revision;
        public StatefulAcceptanceHandler(JsonObject route, params JsonObject[] requests)
        {
            var passengers = requests.Select(request => JsonNode.Parse(CreatePassengerRouteDocumentJson(
                "passenger-route-" + request["name"]!.GetValue<string>().Split('/').Last(),
                request["fields"]!["passengerUserId"]!["stringValue"]!.GetValue<string>()))!.AsObject());
            _documents = requests.Concat(passengers).Append(route).ToDictionary(document =>
                document["name"]!.GetValue<string>().Split("/documents/", StringSplitOptions.None)[1]);
        }

        public Func<string, int, Task>? BeforeCommit { get; set; }
        public string ConflictStatus { get; set; } = "FAILED_PRECONDITION";
        public List<CommitAttempt> Attempts { get; } = [];
        public Dictionary<string, int> RequestReads { get; } = [];
        public List<(string Before, string After)> FailedCommits { get; } = [];
        public int RouteReads { get; private set; }
        public Dictionary<string, int> DocumentReads { get; } = [];
        public JsonObject Fields(string key) => _documents[key]["fields"]!.AsObject();
        public void AddDocument(JsonObject document) => _documents.Add(document["name"]!.GetValue<string>().Split("/documents/", StringSplitOptions.None)[1], document);
        public int Conflicts => FailedCommits.Count;
        public JsonObject RouteFields => _documents["weeklyRoutes/driver-route"]["fields"]!.AsObject();
        public JsonArray Reservations => RouteFields["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray();
        public int Seats(DayOfWeek day) => int.Parse(RouteFields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!
            [day.ToString()]!["integerValue"]!.GetValue<string>(), CultureInfo.InvariantCulture);
        public string Status(string id) => _documents["rideRequests/" + id]["fields"]!["status"]!["stringValue"]!.GetValue<string>();
        public string[] AcceptedDays(string id) => _documents["rideRequests/" + id]["fields"]!["acceptedDays"]!["arrayValue"]!
            ["values"]!.AsArray().Select(value => value!["stringValue"]!.GetValue<string>()).ToArray();
        public CommitAttempt[] AttemptsFor(string id) => Attempts.Where(attempt => attempt.RequestId == id).ToArray();
        public string Snapshot() => string.Join("|", _documents.OrderBy(entry => entry.Key).Select(entry => entry.Value.ToJsonString()));

        public void EditRoute(Action<JsonObject> edit) => Edit("weeklyRoutes/driver-route", edit);
        public void EditRequest(string id, Action<JsonObject> edit) => Edit("rideRequests/" + id, edit);
        private void Edit(string key, Action<JsonObject> edit)
        {
            edit(_documents[key]["fields"]!.AsObject());
            _documents[key]["updateTime"] = NextVersion();
        }
        private string NextVersion() => new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)
            .AddSeconds(++_revision).ToString("O", CultureInfo.InvariantCulture);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                var key = request.RequestUri!.AbsolutePath.Split("/documents/", StringSplitOptions.None)[1];
                DocumentReads[key] = DocumentReads.GetValueOrDefault(key) + 1;
                if (key.StartsWith("rideRequests/", StringComparison.Ordinal))
                {
                    var id = key.Split('/')[1];
                    RequestReads[id] = RequestReads.GetValueOrDefault(id) + 1;
                }
                else if (key.StartsWith("weeklyRoutes/driver-route", StringComparison.Ordinal)) RouteReads++;
                return JsonResponse(_documents[key].ToJsonString());
            }
            Assert.EndsWith(":commit", request.RequestUri!.AbsoluteUri);
            var writes = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!["writes"]!.AsArray();
            var weekly = writes[1]!["update"]!["fields"]!.AsObject().ContainsKey("weeklyAvailableSeats");
            Assert.Equal(weekly ? 3 : 2, writes.Count);
            var requestId = writes[0]!["update"]!["name"]!.GetValue<string>().Split('/').Last();
            var attempt = new CommitAttempt(requestId, writes);
            Attempts.Add(attempt);
            Assert.Equal(new[] { "status", "acceptedDays" }, writes[0]!["updateMask"]!["fieldPaths"]!.AsArray().Select(value => value!.GetValue<string>()));
            if (BeforeCommit is not null) await BeforeCommit(requestId, AttemptsFor(requestId).Length);

            var before = Snapshot();
            foreach (var write in writes)
            {
                var key = write!["update"]!["name"]!.GetValue<string>().Split("/documents/", StringSplitOptions.None)[1];
                var expected = write["currentDocument"]!["updateTime"]!.GetValue<string>();
                if (expected != _documents[key]["updateTime"]!.GetValue<string>())
                {
                    FailedCommits.Add((before, Snapshot()));
                    var failure = JsonResponse("{\"error\":{\"status\":\"" + ConflictStatus + "\",\"message\":\"version conflict\"}}");
                    failure.StatusCode = HttpStatusCode.Conflict;
                    return failure;
                }
            }

            var updated = new Dictionary<string, JsonObject>();
            var version = NextVersion();
            foreach (var write in writes)
            {
                var key = write!["update"]!["name"]!.GetValue<string>().Split("/documents/", StringSplitOptions.None)[1];
                var document = _documents[key].DeepClone().AsObject();
                foreach (var field in write["updateMask"]!["fieldPaths"]!.AsArray())
                {
                    var name = field!.GetValue<string>();
                    document["fields"]![name] = write["update"]!["fields"]![name]!.DeepClone();
                }
                document["updateTime"] = version;
                updated.Add(key, document);
            }
            foreach (var document in updated) _documents[document.Key] = document.Value;
            attempt.Committed = true;
            return JsonResponse("{}");
        }

        public void AssertConsistent()
        {
            Assert.All(FailedCommits, commit => Assert.Equal(commit.Before, commit.After));
            var capacity = int.Parse(RouteFields["seatCapacity"]!["integerValue"]!.GetValue<string>(), CultureInfo.InvariantCulture);
            foreach (var day in RouteFields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!.AsObject())
            {
                Assert.InRange(Seats(Enum.Parse<DayOfWeek>(day.Key)), 0, capacity);
                var weeklyOccupants = _documents.Where(entry => entry.Key.StartsWith("rideRequests/", StringComparison.Ordinal))
                    .Count(entry => entry.Value["fields"]!["type"]!["stringValue"]!.GetValue<string>() == "weekly"
                        && entry.Value["fields"]!["driverRouteId"]!["stringValue"]!.GetValue<string>() == "driver-route"
                        && Status(entry.Key.Split('/')[1]) == "accepted"
                        && AcceptedDays(entry.Key.Split('/')[1]).Contains(day.Key));
                Assert.True(Seats(Enum.Parse<DayOfWeek>(day.Key)) + weeklyOccupants <= capacity);
            }
            var reservations = Reservations.Select(value => value!["mapValue"]!["fields"]!).ToArray();
            Assert.Equal(reservations.Length, reservations.Select(value => value["requestId"]!["stringValue"]!.GetValue<string>()).Distinct().Count());
            foreach (var group in reservations.GroupBy(value => DateOnly.ParseExact(value["date"]!["stringValue"]!.GetValue<string>(), "yyyy-MM-dd", CultureInfo.InvariantCulture)))
                Assert.True(Seats(group.Key.DayOfWeek) - group.Count() >= 0);
            foreach (var entry in _documents.Where(entry => entry.Key.StartsWith("rideRequests/", StringComparison.Ordinal)))
            {
                var id = entry.Key.Split('/')[1];
                var fields = entry.Value["fields"]!;
                if (Status(id) != "accepted") { Assert.Empty(AcceptedDays(id)); continue; }
                if (fields["type"]!["stringValue"]!.GetValue<string>() == "weekly")
                {
                    Assert.NotEmpty(AcceptedDays(id));
                    Assert.Equal(AcceptedDays(id).Length, AcceptedDays(id).Distinct().Count());
                    Assert.All(AcceptedDays(id), day => Assert.Contains(day, fields["compatibleDays"]!["arrayValue"]!["values"]!.AsArray().Select(value => value!["stringValue"]!.GetValue<string>())));
                }
                else
                {
                    Assert.Empty(AcceptedDays(id));
                    Assert.Single(reservations.Where(value => value["requestId"]!["stringValue"]!.GetValue<string>() == id));
                }
            }
        }
    }
}
