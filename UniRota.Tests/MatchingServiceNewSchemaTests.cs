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
        if (matches) Assert.Equal(new[] { DayOfWeek.Monday }, Assert.Single(result).CompatibleDays);
    }

    [Theory]
    [InlineData(2020, 1, 6)]
    [InlineData(2099, 1, 5)]
    public async Task FindMatchesAsync_PastOrFutureOnceReservationsDoNotChangeCompatibility(
        int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var weekly = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 1,
            [DayOfWeek.Tuesday] = 0
        };
        var passenger = CreatePassenger(days: [DayOfWeek.Monday, DayOfWeek.Tuesday]);
        var service = CreateService(new FakeMapRouteService());
        var baseline = Assert.Single(await service.FindMatchesAsync(
            passenger, [NewSchemaCandidate(weekly)]));
        var driver = NewSchemaCandidate(weekly, [new OneTimeReservation
        {
            RequestId = "once-A",
            Date = date,
            DayOfWeek = date.DayOfWeek
        }, new OneTimeReservation
        {
            RequestId = "once-B",
            Date = date.AddDays(7),
            DayOfWeek = date.AddDays(7).DayOfWeek
        }]);

        // Mesmo lotando datas específicas, a busca não recebe tipo/data de solicitação.
        var match = Assert.Single(await service.FindMatchesAsync(passenger, [driver]));

        Assert.Equal(new[] { DayOfWeek.Monday }, match.CompatibleDays);
        Assert.Equal(baseline.CompatibleDays, match.CompatibleDays);
        Assert.Equal(baseline.TimeDifferenceMinutes, match.TimeDifferenceMinutes);
        Assert.Equal(baseline.SharedEncodedPolyline, match.SharedEncodedPolyline);
    }

    [Theory]
    [InlineData(0, 2, false)]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    public async Task FindMatchesAsync_RequiresPositiveBalanceInsideDayIntersection(
        int mondaySeats, int tuesdaySeats, bool matches)
    {
        var map = new FakeMapRouteService();
        var driver = NewSchemaCandidate(new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = mondaySeats,
            [DayOfWeek.Tuesday] = tuesdaySeats
        });

        var result = await CreateService(map).FindMatchesAsync(
            CreatePassenger(days: [DayOfWeek.Monday]), [driver]);

        Assert.Equal(matches ? 1 : 0, result.Count);
        Assert.Equal(matches ? 2 : 0, map.Requests.Count);
        if (matches) Assert.Equal(new[] { DayOfWeek.Monday }, Assert.Single(result).CompatibleDays);
    }

    [Fact]
    public async Task FindMatchesAsync_ExcludesFullDaysAndKeepsOnlyPositiveCommonDaysInOrder()
    {
        var driver = NewSchemaCandidate(new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 0,
            [DayOfWeek.Tuesday] = 2,
            [DayOfWeek.Wednesday] = 1,
            [DayOfWeek.Thursday] = 3
        }, days: [DayOfWeek.Thursday, DayOfWeek.Wednesday, DayOfWeek.Monday, DayOfWeek.Tuesday]);

        var match = Assert.Single(await CreateService(new FakeMapRouteService()).FindMatchesAsync(
            CreatePassenger(days: [DayOfWeek.Wednesday, DayOfWeek.Monday, DayOfWeek.Tuesday]), [driver]));

        Assert.Equal(new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday }, match.CompatibleDays);
    }

    private static WeeklyRoute NewSchemaCandidate(
        IReadOnlyDictionary<DayOfWeek, int> weekly,
        IReadOnlyList<OneTimeReservation>? reservations = null,
        IReadOnlyList<DayOfWeek>? days = null) => new()
    {
        Id = "new-schema",
        UserId = "driver-user",
        Role = RouteRole.Driver,
        Origin = "Origem do motorista",
        OriginPlaceId = "driver-origin",
        Destination = "Destino do motorista",
        DestinationPlaceId = "driver-destination",
        DaysOfWeek = days ?? [DayOfWeek.Monday, DayOfWeek.Tuesday],
        DepartureTimeMinutes = DefaultDepartureTimeMinutes,
        SeatCapacity = 3,
        WeeklyAvailableSeats = weekly,
        OneTimeReservations = reservations ?? []
    };
}
