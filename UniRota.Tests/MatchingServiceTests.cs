using System.Globalization;
using UniRota.Models;
using UniRota.Services;

namespace UniRota.Tests;

public sealed class MatchingServiceTests
{
    private const int DefaultDepartureTimeMinutes = 480;

    private readonly MatchingService _service = new();

    [Fact]
    public void FindMatches_ReturnsMatch_WhenAllCriteriaAreCompatible()
    {
        var passenger = CreatePassenger();
        var driver = CreateDriver();

        var match = Assert.Single(_service.FindMatches(passenger, [driver]));

        Assert.Same(driver, match.DriverRoute);
        Assert.Equal([DayOfWeek.Monday], match.CompatibleDays);
        Assert.Equal(0, match.TimeDifferenceMinutes);
    }

    [Fact]
    public void FindMatches_IgnoresCandidate_WhenCandidateIsNotDriver()
    {
        var candidate = CreateDriver(role: RouteRole.Passenger);

        var matches = _service.FindMatches(CreatePassenger(), [candidate]);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_IgnoresCandidate_WhenRoutesBelongToSameUser()
    {
        var passenger = CreatePassenger(userId: "same-user");
        var driver = CreateDriver(userId: "same-user");

        var matches = _service.FindMatches(passenger, [driver]);

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void FindMatches_IgnoresCandidate_WhenSeatsAreNotAvailable(int? seats)
    {
        var driver = CreateDriver(availableSeats: seats);

        var matches = _service.FindMatches(CreatePassenger(), [driver]);

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData("Centro", "Facens", "Outro bairro", "Facens")]
    [InlineData("Centro", "Facens", "Centro", "Outro destino")]
    [InlineData("Facens", "Centro", "Faculdade de Engenharia de Sorocaba", "Centro")]
    [InlineData("Centro", "Facens", "Centro Sorocaba", "Facens")]
    public void FindMatches_IgnoresCandidate_WhenLocationsAreDifferent(
        string passengerOrigin,
        string passengerDestination,
        string driverOrigin,
        string driverDestination)
    {
        var passenger = CreatePassenger(
            origin: passengerOrigin,
            destination: passengerDestination);
        var driver = CreateDriver(
            origin: driverOrigin,
            destination: driverDestination);

        var matches = _service.FindMatches(passenger, [driver]);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_IgnoresCandidate_WhenThereIsNoCompatibleDay()
    {
        var passenger = CreatePassenger(days: [DayOfWeek.Monday]);
        var driver = CreateDriver(days: [DayOfWeek.Tuesday]);

        var matches = _service.FindMatches(passenger, [driver]);

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void FindMatches_AppliesInclusiveTimeBoundary(
        int differenceMinutes,
        bool shouldMatch)
    {
        var passenger = CreatePassenger(departureTimeMinutes: 600);
        var driver = CreateDriver(
            departureTimeMinutes: 600 + differenceMinutes);

        var matches = _service.FindMatches(passenger, [driver]);

        Assert.Equal(shouldMatch, matches.Count == 1);
    }

    [Fact]
    public void FindMatches_DoesNotUseCircularTimeDifferenceAcrossMidnight()
    {
        var passenger = CreatePassenger(departureTimeMinutes: 1430);
        var driver = CreateDriver(departureTimeMinutes: 10);

        var matches = _service.FindMatches(passenger, [driver]);

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData(" Éden ", "eden")]
    [InlineData("Jardim   Europa", " jardim europa ")]
    [InlineData("São Bento", "sao bento")]
    public void FindMatches_NormalizesCaseWhitespaceAndDiacritics(
        string passengerOrigin,
        string driverOrigin)
    {
        var passenger = CreatePassenger(origin: passengerOrigin);
        var driver = CreateDriver(origin: driverOrigin);

        var matches = _service.FindMatches(passenger, [driver]);

        Assert.Single(matches);
    }

    [Fact]
    public void FindMatches_AppliesTheSameNormalizationToDestination()
    {
        var passenger = CreatePassenger(destination: " São Bento ");
        var driver = CreateDriver(destination: "sao   bento");

        var matches = _service.FindMatches(passenger, [driver]);

        Assert.Single(matches);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("tr-TR")]
    public void FindMatches_NormalizationDoesNotDependOnCurrentCulture(
        string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

            var passenger = CreatePassenger(origin: " São Bento ");
            var driver = CreateDriver(origin: "sao   bento");

            Assert.Single(_service.FindMatches(passenger, [driver]));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void FindMatches_ReturnsDistinctOrderedCompatibleDays()
    {
        var passenger = CreatePassenger(
            days:
            [
                DayOfWeek.Monday,
                DayOfWeek.Wednesday,
                DayOfWeek.Friday,
                DayOfWeek.Monday
            ]);
        var driver = CreateDriver(
            days:
            [
                DayOfWeek.Friday,
                DayOfWeek.Thursday,
                DayOfWeek.Monday,
                DayOfWeek.Friday
            ]);

        var match = Assert.Single(_service.FindMatches(passenger, [driver]));

        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Friday],
            match.CompatibleDays);
    }

    [Fact]
    public void FindMatches_OrdersByTimeDifferenceFirst()
    {
        var passenger = CreatePassenger(departureTimeMinutes: 600);
        var fartherDriver = CreateDriver(id: "a", departureTimeMinutes: 620);
        var closerDriver = CreateDriver(id: "z", departureTimeMinutes: 605);

        var matches = _service.FindMatches(
            passenger,
            [fartherDriver, closerDriver]);

        Assert.Equal(
            ["z", "a"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public void FindMatches_OrdersByCompatibleDayCountSecond()
    {
        var passenger = CreatePassenger(
            days: [DayOfWeek.Monday, DayOfWeek.Friday]);
        var fewerDays = CreateDriver(
            id: "a",
            days: [DayOfWeek.Monday]);
        var moreDays = CreateDriver(
            id: "z",
            days: [DayOfWeek.Monday, DayOfWeek.Friday]);

        var matches = _service.FindMatches(passenger, [fewerDays, moreDays]);

        Assert.Equal(
            ["z", "a"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public void FindMatches_OrdersByDriverDepartureTimeThird()
    {
        var passenger = CreatePassenger(departureTimeMinutes: 600);
        var laterDriver = CreateDriver(id: "a", departureTimeMinutes: 610);
        var earlierDriver = CreateDriver(id: "z", departureTimeMinutes: 590);

        var matches = _service.FindMatches(
            passenger,
            [laterDriver, earlierDriver]);

        Assert.Equal(
            ["z", "a"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public void FindMatches_OrdersByRouteIdOrdinallyAsFinalTieBreaker()
    {
        var passenger = CreatePassenger();
        var lowercaseId = CreateDriver(id: "a");
        var uppercaseId = CreateDriver(id: "A");
        var emptyId = CreateDriver(id: string.Empty);

        var matches = _service.FindMatches(
            passenger,
            [lowercaseId, emptyId, uppercaseId]);

        Assert.Equal(
            [string.Empty, "A", "a"],
            matches.Select(match => match.DriverRoute.Id));
    }

    [Fact]
    public void FindMatches_ThrowsForNullPassengerRoute()
    {
        Assert.Throws<ArgumentNullException>(
            () => _service.FindMatches(null!, []));
    }

    [Fact]
    public void FindMatches_ThrowsForNullCandidateCollection()
    {
        Assert.Throws<ArgumentNullException>(
            () => _service.FindMatches(CreatePassenger(), null!));
    }

    [Fact]
    public void FindMatches_ThrowsWhenReferenceRouteIsDriver()
    {
        var invalidReference = CreatePassenger(role: RouteRole.Driver);

        Assert.Throws<ArgumentException>(
            () => _service.FindMatches(invalidReference, []));
    }

    [Fact]
    public void FindMatches_ThrowsWhenReferenceUserIdIsEmpty()
    {
        var invalidReference = CreatePassenger(userId: "   ");

        Assert.Throws<ArgumentException>(
            () => _service.FindMatches(invalidReference, []));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1440)]
    public void FindMatches_ThrowsWhenReferenceTimeIsInvalid(
        int departureTimeMinutes)
    {
        var invalidReference = CreatePassenger(
            departureTimeMinutes: departureTimeMinutes);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.FindMatches(invalidReference, []));
    }

    [Fact]
    public void FindMatches_ThrowsWhenReferenceOriginIsEmpty()
    {
        var invalidReference = CreatePassenger(origin: "   ");

        Assert.Throws<ArgumentException>(
            () => _service.FindMatches(invalidReference, []));
    }

    [Fact]
    public void FindMatches_ThrowsWhenReferenceDestinationIsEmpty()
    {
        var invalidReference = CreatePassenger(destination: "   ");

        Assert.Throws<ArgumentException>(
            () => _service.FindMatches(invalidReference, []));
    }

    [Fact]
    public void FindMatches_ThrowsWhenReferenceHasNoDays()
    {
        var invalidReference = CreatePassenger(days: []);

        Assert.Throws<ArgumentException>(
            () => _service.FindMatches(invalidReference, []));
    }

    [Fact]
    public void FindMatches_IgnoresMalformedCandidatesAndKeepsValidOnes()
    {
        var malformedCandidates = new WeeklyRoute[]
        {
            null!,
            CreateDriver(id: "wrong-role", role: RouteRole.Passenger),
            CreateDriver(id: "empty-user", userId: " "),
            CreateDriver(id: "empty-origin", origin: " "),
            CreateDriver(id: "empty-destination", destination: " "),
            CreateDriver(id: "no-days", days: []),
            CreateDriver(id: "invalid-day", days: [(DayOfWeek)99]),
            CreateDriver(id: "invalid-time", departureTimeMinutes: 1440),
            CreateDriver(id: "no-seats", availableSeats: 0),
            CreateDriver(id: "valid")
        };

        var match = Assert.Single(
            _service.FindMatches(CreatePassenger(), malformedCandidates));

        Assert.Equal("valid", match.DriverRoute.Id);
    }

    private static WeeklyRoute CreatePassenger(
        string id = "passenger-route",
        string userId = "passenger-user",
        RouteRole role = RouteRole.Passenger,
        string origin = "Éden",
        string destination = "Facens",
        IReadOnlyList<DayOfWeek>? days = null,
        int departureTimeMinutes = DefaultDepartureTimeMinutes)
    {
        return new WeeklyRoute
        {
            Id = id,
            UserId = userId,
            UserName = "Passageiro",
            Role = role,
            Origin = origin,
            Destination = destination,
            DaysOfWeek = days ?? [DayOfWeek.Monday],
            DepartureTimeMinutes = departureTimeMinutes,
            AvailableSeats = null,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
    }

    private static WeeklyRoute CreateDriver(
        string id = "driver-route",
        string userId = "driver-user",
        RouteRole role = RouteRole.Driver,
        string origin = "Eden",
        string destination = "Facens",
        IReadOnlyList<DayOfWeek>? days = null,
        int departureTimeMinutes = DefaultDepartureTimeMinutes,
        int? availableSeats = 1)
    {
        return new WeeklyRoute
        {
            Id = id,
            UserId = userId,
            UserName = "Motorista",
            Role = role,
            Origin = origin,
            Destination = destination,
            DaysOfWeek = days ?? [DayOfWeek.Monday],
            DepartureTimeMinutes = departureTimeMinutes,
            AvailableSeats = availableSeats,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
    }
}
