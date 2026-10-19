using System.Text.Json.Nodes;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseRouteServiceTests
{
    [Theory]
    [InlineData(true, "2026-10-28", true)]
    [InlineData(false, "2026-10-28", false)]
    [InlineData(true, "2026-10-28,2026-10-28", false)]
    [InlineData(true, "2026-1-01", false)]
    [InlineData(true, "28/10/2026", false)]
    [InlineData(true, "2026-02-30", false)]
    [InlineData(true, "2026-10-28T00:00:00Z", false)]
    [InlineData(true, "missing", false)]
    [InlineData(true, "wrong-type", false)]
    public async Task OneTimeAcceptedDates_RequiresCanonicalUniqueDateArray(bool passenger, string dates, bool valid)
    {
        var document = CreateSeatDocument(passenger);
        if (dates == "missing") document["fields"]!.AsObject().Remove("oneTimeAcceptedDates");
        else document["fields"]!["oneTimeAcceptedDates"] = dates == "wrong-type"
            ? new JsonObject { ["stringValue"] = "2026-10-28" } : Ledger(dates.Split(','));
        if (valid)
        {
            var result = Assert.Single(await ReadSeatDocumentAsync(document));
            Assert.Equal(new[] { new DateOnly(2026, 10, 28) }, result.OneTimeAcceptedDates);
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSeatDocumentAsync(document));
    }
}
