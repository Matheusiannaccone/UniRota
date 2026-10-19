using System.Text.Json.Nodes;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseRouteServiceTests
{
    [Theory]
    [InlineData(RouteRole.Passenger)]
    [InlineData(RouteRole.Driver)]
    public async Task NewRoute_PersistsEmptyWeeklyAcceptedDays(RouteRole role)
    {
        var input = CreateSeatSchemaRoute(role: role, capacity: role == RouteRole.Driver ? 3 : null);
        var (route, fields, _) = await RoundTripSeatRouteAsync(input);
        Assert.Empty(route.WeeklyAcceptedDays);
        Assert.Empty(fields["weeklyAcceptedDays"]!["arrayValue"]!["values"]!.AsArray());
    }

    [Theory]
    [InlineData(true, "Monday", true)]
    [InlineData(false, "Monday", false)]
    [InlineData(true, "Monday,Monday", false)]
    [InlineData(true, "Friday", false)]
    [InlineData(true, "monday", false)]
    [InlineData(true, "1", false)]
    [InlineData(true, "missing", false)]
    public async Task ReadRoute_RequiresCanonicalUniqueWeeklyAcceptedDaysSubset(bool passenger, string value, bool valid)
    {
        var document = CreateSeatDocument(passenger);
        if (value == "missing") document["fields"]!.AsObject().Remove("weeklyAcceptedDays");
        else document["fields"]!["weeklyAcceptedDays"] = Ledger(value.Split(','));
        if (valid)
        {
            var route = Assert.Single(await ReadSeatDocumentAsync(document));
            Assert.Equal(new[] { DayOfWeek.Monday }, route.WeeklyAcceptedDays);
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSeatDocumentAsync(document));
    }

    [Fact]
    public async Task PassengerEdit_PreservesConfirmedDaysFromFreshDocument()
    {
        var document = CreateSeatDocument(passenger: true);
        document["fields"]!["weeklyAcceptedDays"] = Ledger(["Monday"]);
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get) return JsonResponse(document.ToJsonString());
            var fields = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!["fields"]!;
            Assert.Equal("Monday", fields["weeklyAcceptedDays"]!["arrayValue"]!["values"]![0]!["stringValue"]!.GetValue<string>());
            foreach (var entry in fields.AsObject()) document["fields"]![entry.Key] = entry.Value!.DeepClone();
            return JsonResponse(document.ToJsonString());
        });
        var result = await CreateService(handler).UpdateAsync(CreateSeatSchemaRoute(role: RouteRole.Passenger, capacity: null));
        Assert.Equal(new[] { DayOfWeek.Monday }, result.WeeklyAcceptedDays);
    }

    private static JsonObject Ledger(IEnumerable<string> days) => new()
    {
        ["arrayValue"] = new JsonObject
        { ["values"] = new JsonArray(days.Select(day => (JsonNode)new JsonObject { ["stringValue"] = day }).ToArray()) }
    };
}
