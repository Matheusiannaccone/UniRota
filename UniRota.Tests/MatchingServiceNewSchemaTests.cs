using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class MatchingServiceTests
{
    [Theory]
    [InlineData("positive", true)]
    [InlineData("all-zero", false)]
    [InlineData("missing-day", false)]
    [InlineData("extra-day", false)]
    [InlineData("negative", false)]
    [InlineData("above-capacity", false)]
    [InlineData("null-map", false)]
    public async Task FindMatchesAsync_UsesWeeklySchemaInsteadOfLegacyBalance(string scenario, bool matches)
    {
        var weekly = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 2,
            [DayOfWeek.Tuesday] = 0
        };
        switch (scenario)
        {
            case "all-zero": weekly[DayOfWeek.Monday] = 0; break;
            case "missing-day": weekly.Remove(DayOfWeek.Tuesday); break;
            case "extra-day": weekly[DayOfWeek.Wednesday] = 1; break;
            case "negative": weekly[DayOfWeek.Tuesday] = -1; break;
            case "above-capacity": weekly[DayOfWeek.Monday] = 4; break;
        }
        var driver = NewSchemaCandidate(scenario == "null-map" ? null! : weekly);
        var map = new FakeMapRouteService();

        var result = await CreateService(map).FindMatchesAsync(
            CreatePassenger(days: [DayOfWeek.Monday, DayOfWeek.Tuesday]), [driver]);

        Assert.Equal(matches ? 1 : 0, result.Count);
        Assert.Equal(matches ? 2 : 0, map.Requests.Count);
    }

    [Fact]
    public async Task FindMatchesAsync_TransitionDoesNotFilterDaysOrCountOnceReservations()
    {
        var driver = NewSchemaCandidate(new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 1,
            [DayOfWeek.Tuesday] = 0
        }, [new OneTimeReservation
        {
            RequestId = "once-A",
            Date = new DateOnly(2026, 10, 19),
            DayOfWeek = DayOfWeek.Monday
        }]);

        var result = await CreateService(new FakeMapRouteService()).FindMatchesAsync(
            CreatePassenger(days: [DayOfWeek.Monday, DayOfWeek.Tuesday]), [driver]);

        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }, Assert.Single(result).CompatibleDays);
    }

    private static WeeklyRoute NewSchemaCandidate(
        IReadOnlyDictionary<DayOfWeek, int> weekly,
        IReadOnlyList<OneTimeReservation>? reservations = null) => new()
    {
        Id = "new-schema",
        UserId = "driver-user",
        Role = RouteRole.Driver,
        Origin = "Origem do motorista",
        OriginPlaceId = "driver-origin",
        Destination = "Destino do motorista",
        DestinationPlaceId = "driver-destination",
        DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday],
        DepartureTimeMinutes = DefaultDepartureTimeMinutes,
        SeatCapacity = 3,
        WeeklyAvailableSeats = weekly,
        OneTimeReservations = reservations ?? []
    };
}
