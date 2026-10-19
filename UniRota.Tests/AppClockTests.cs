using System.Globalization;
using UniRota.Services;

namespace UniRota.Tests;

public sealed class AppClockTests
{
    [Theory]
    [InlineData("2026-10-19T02:59:59Z", 2026, 10, 18)]
    [InlineData("2026-10-19T03:00:00Z", 2026, 10, 19)]
    [InlineData("2026-10-20T02:59:59Z", 2026, 10, 19)]
    [InlineData("2027-01-01T02:59:59Z", 2026, 12, 31)]
    public void Today_UsesSaoPauloCivilDateRegardlessOfLocalTimeZone(
        string utcInstant, int year, int month, int day)
    {
        var clock = new AppClock(new FixedTimeProvider(
            DateTimeOffset.Parse(utcInstant, CultureInfo.InvariantCulture)));

        Assert.Equal(new DateOnly(year, month, day), clock.Today);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
