using System.Text.Json.Nodes;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseRideRequestServicePricingTests
{
    [Theory]
    [InlineData("weekly", "pending", "", true)]
    [InlineData("weekly", "accepted", "Tuesday,Wednesday", true)]
    [InlineData("once", "accepted", "", true)]
    [InlineData("weekly", "accepted", "", false)]
    [InlineData("weekly", "pending", "Monday", false)]
    [InlineData("weekly", "rejected", "Monday", false)]
    [InlineData("once", "accepted", "Monday", false)]
    [InlineData("weekly", "accepted", "Tuesday,Tuesday", false)]
    [InlineData("weekly", "accepted", "Friday", false)]
    [InlineData("weekly", "accepted", "monday", false)]
    [InlineData("weekly", "accepted", "1", false)]
    [InlineData("weekly", "pending", "missing", false)]
    [InlineData("weekly", "pending", "invalid-type", false)]
    public async Task ReadRequest_EnforcesAcceptedDaysContract(
        string type, string status, string accepted, bool valid)
    {
        var document = JsonNode.Parse(CreateRideRequestDocumentJson("request-1", "passenger-user", 3.45m))!;
        var fields = document["fields"]!;
        fields["compatibleDays"] = DaysField(["Monday", "Tuesday", "Wednesday"]);
        fields["type"]!["stringValue"] = type;
        fields["status"]!["stringValue"] = status;
        if (type == "once") fields["requestedDate"] = new JsonObject { ["stringValue"] = "2026-10-19" };
        if (accepted == "missing") fields.AsObject().Remove("acceptedDays");
        else if (accepted == "invalid-type") fields["acceptedDays"] = new JsonObject { ["stringValue"] = "Monday" };
        else fields["acceptedDays"] = DaysField(accepted.Length == 0 ? [] : accepted.Split(','));

        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("[{\"document\":" + document.ToJsonString() + "}]")));
        var service = CreateService(handler, []);
        var read = () => status == "accepted"
            ? service.GetMyAcceptedRequestsAsync()
            : service.GetMyPendingRequestsAsync();

        if (!valid)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(read);
            Assert.Contains("acceptedDays", exception.Message);
            return;
        }

        var request = Assert.Single(await read());
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday }, request.CompatibleDays);
        Assert.Equal(accepted.Length == 0 ? [] : accepted.Split(','),
            request.AcceptedDays.Select(day => day.ToString()));
        Assert.Equal(3.45m, request.SuggestedPrice);
    }

    private static JsonObject DaysField(IEnumerable<string> days) => new()
    {
        ["arrayValue"] = new JsonObject
        {
            ["values"] = new JsonArray(days.Select(day => (JsonNode)new JsonObject { ["stringValue"] = day }).ToArray())
        }
    };
}
