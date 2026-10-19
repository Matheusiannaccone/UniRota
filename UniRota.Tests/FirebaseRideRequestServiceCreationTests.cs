using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseRideRequestServicePricingTests
{
    [Fact]
    public async Task CreateAsync_WeeklyUsesNewSchemaAndCommitsOnlyPendingAndRevision()
    {
        var weekly = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 2,
            [DayOfWeek.Tuesday] = 1
        };

        await AssertCreationAsync(RideRequestType.Weekly, weekly, [], null, allowed: true);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    public async Task CreateAsync_WeeklyRejectsIfAnyRequestedDayIsFull(DayOfWeek fullDay)
    {
        var weekly = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 2,
            [DayOfWeek.Tuesday] = 2
        };
        weekly[fullDay] = 0;

        await AssertCreationAsync(RideRequestType.Weekly, weekly, [], null, allowed: false);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, true)]
    [InlineData(2, 0, false)]
    [InlineData(0, 2, true)]
    [InlineData(1, 2, true)]
    public async Task CreateAsync_OnceCountsOnlyReservationsOnRequestedDate(
        int sameDateCount, int otherDateCount, bool allowed)
    {
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(7);
        var otherDay = (DayOfWeek)(((int)date.DayOfWeek + 1) % 7);
        var weekly = new Dictionary<DayOfWeek, int>
        {
            [otherDay] = 0,
            [date.DayOfWeek] = 2
        };
        var reservations = Enumerable.Range(0, sameDateCount + otherDateCount)
            .Select(index =>
            {
                var reservationDate = index < sameDateCount ? date : date.AddDays(7);
                return new OneTimeReservation
                {
                    RequestId = "reservation-" + index,
                    Date = reservationDate,
                    DayOfWeek = reservationDate.DayOfWeek
                };
            }).ToArray();

        await AssertCreationAsync(RideRequestType.Once, weekly, reservations, date, allowed);
    }

    [Theory]
    [InlineData(RideRequestType.Weekly)]
    [InlineData(RideRequestType.Once)]
    public async Task CreateAsync_NewSchemaStillBlocksDuplicatePending(RideRequestType type)
    {
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(7);
        var weekly = new Dictionary<DayOfWeek, int> { [date.DayOfWeek] = 2 };

        await AssertCreationAsync(type, weekly, [], type == RideRequestType.Once ? date : null,
            allowed: false, duplicate: true);
    }

    private static async Task AssertCreationAsync(
        RideRequestType type,
        IReadOnlyDictionary<DayOfWeek, int> weekly,
        IReadOnlyList<OneTimeReservation> reservations,
        DateOnly? date,
        bool allowed,
        bool duplicate = false)
    {
        var routeJson = CreateCreationDriverRouteDocumentJson(weekly, reservations, revision: 7);
        Assert.False(JsonNode.Parse(routeJson)!["fields"]!.AsObject().ContainsKey("availableSeats"));
        string? commitBody = null;
        var commits = 0;
        var reads = 0;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (request.Method == HttpMethod.Get && url.Contains("/weeklyRoutes/driver-route", StringComparison.Ordinal))
            {
                reads++;
                return JsonResponse(routeJson);
            }

            if (url.EndsWith(":runQuery", StringComparison.Ordinal))
            {
                return JsonResponse(duplicate ? CreateRideRequestQueryJson() : "[]");
            }

            if (url.EndsWith(":commit", StringComparison.Ordinal))
            {
                commits++;
                commitBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return JsonResponse("{}");
            }

            throw new InvalidOperationException($"Requisição inesperada: {url}");
        });
        var service = CreateService(handler, [CreatePassengerRoute()]);
        // O snapshot do match não contém saldos: a decisão deve usar a rota relida.
        var match = new MatchResult(new WeeklyRoute
        {
            Id = "driver-route",
            UserId = "driver-user",
            Role = RouteRole.Driver
        }, weekly.Keys.ToArray(), 0);

        if (!allowed)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync("passenger-route", match, type, date, 3.45m));
            Assert.Contains(duplicate ? "pendente" : "vagas", exception.Message);
            Assert.Equal(0, commits);
            Assert.Equal(1, reads);
            return;
        }

        var created = await service.CreateAsync("passenger-route", match, type, date, 3.45m);

        Assert.Equal(1, reads);
        Assert.Equal(1, commits);
        Assert.Equal(RideRequestStatus.Pending, created.Status);
        Assert.Equal(type, created.Type);
        Assert.Equal(date, created.RequestedDate);
        Assert.Equal(3.45m, created.SuggestedPrice);
        using var json = JsonDocument.Parse(commitBody!);
        var writes = json.RootElement.GetProperty("writes");
        Assert.Equal(2, writes.GetArrayLength());
        Assert.Equal("pending", GetWrittenStringField(writes[0], "status"));
        Assert.Equal(3.45m, writes[0].GetProperty("update").GetProperty("fields")
            .GetProperty("suggestedPrice").GetProperty("doubleValue").GetDecimal());
        var routeFields = writes[1].GetProperty("update").GetProperty("fields");
        Assert.Single(routeFields.EnumerateObject());
        Assert.Equal("8", routeFields.GetProperty("requestRevision").GetProperty("integerValue").GetString());
        Assert.Equal(new[] { "requestRevision" }, writes[1].GetProperty("updateMask")
            .GetProperty("fieldPaths").EnumerateArray().Select(field => field.GetString()));
        Assert.Equal("2026-09-03T12:00:00Z", writes[1].GetProperty("currentDocument")
            .GetProperty("updateTime").GetString());
    }

    private static string CreateCreationDriverRouteDocumentJson(
        IReadOnlyDictionary<DayOfWeek, int>? weekly = null,
        IReadOnlyList<OneTimeReservation>? reservations = null,
        long revision = 0)
    {
        weekly ??= new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 2 };
        reservations ??= [];
        var document = JsonNode.Parse(CreateDriverRouteDocumentJson())!;
        var fields = document["fields"]!.AsObject();
        fields.Remove("availableSeats");
        fields["seatCapacity"] = new JsonObject { ["integerValue"] = "3" };
        fields["requestRevision"] = new JsonObject { ["integerValue"] = revision.ToString(CultureInfo.InvariantCulture) };
        var days = new JsonObject();
        foreach (var entry in weekly)
        {
            days[entry.Key.ToString()] = new JsonObject { ["integerValue"] = entry.Value.ToString(CultureInfo.InvariantCulture) };
        }

        fields["weeklyAvailableSeats"] = new JsonObject { ["mapValue"] = new JsonObject { ["fields"] = days } };
        var values = new JsonArray();
        foreach (var reservation in reservations)
        {
            values.Add(new JsonObject
            {
                ["mapValue"] = new JsonObject
                {
                    ["fields"] = new JsonObject
                    {
                        ["requestId"] = new JsonObject { ["stringValue"] = reservation.RequestId },
                        ["date"] = new JsonObject { ["stringValue"] = reservation.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                        ["dayOfWeek"] = new JsonObject { ["stringValue"] = reservation.DayOfWeek.ToString() }
                    }
                }
            });
        }

        fields["oneTimeReservations"] = new JsonObject { ["arrayValue"] = new JsonObject { ["values"] = values } };
        return document.ToJsonString();
    }
}
