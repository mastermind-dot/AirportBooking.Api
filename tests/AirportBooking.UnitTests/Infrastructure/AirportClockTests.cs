using AirportBooking.Infrastructure.Common;

namespace AirportBooking.UnitTests.Infrastructure;

/// <summary>
/// Every departure time a customer reads is produced by this class. It is also
/// the only place that handles the hour that does not exist on the spring
/// forward date — the case that throws if you convert naively, and which no
/// amount of manual testing will hit unless you are working in late March.
/// </summary>
public class AirportClockTests
{
    private static readonly TimeZoneInfo Goma = TimeZoneInfo.FindSystemTimeZoneById("Africa/Lubumbashi");
    private static readonly TimeZoneInfo Kinshasa = TimeZoneInfo.FindSystemTimeZoneById("Africa/Kinshasa");
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    [Fact]
    public void Goma_is_two_hours_ahead_of_UTC_all_year()
    {
        // Central Africa Time has no daylight saving, which is why an eastern
        // DRC schedule can be generated without worrying about the date.
        var january = AirportClock.ToUtc(new DateTime(2026, 1, 15, 7, 30, 0), Goma);
        var july = AirportClock.ToUtc(new DateTime(2026, 7, 15, 7, 30, 0), Goma);

        Assert.Equal(new DateTime(2026, 1, 15, 5, 30, 0, DateTimeKind.Utc), january);
        Assert.Equal(new DateTime(2026, 7, 15, 5, 30, 0, DateTimeKind.Utc), july);
    }

    [Fact]
    public void Kinshasa_is_one_hour_ahead_so_the_two_DRC_zones_differ()
    {
        // The country spans two zones. Treating them as one would show every
        // western departure an hour out.
        var kinshasa = AirportClock.ToUtc(new DateTime(2026, 9, 7, 9, 0, 0), Kinshasa);
        var goma = AirportClock.ToUtc(new DateTime(2026, 9, 7, 9, 0, 0), Goma);

        Assert.Equal(new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc), kinshasa);
        Assert.Equal(new DateTime(2026, 9, 7, 7, 0, 0, DateTimeKind.Utc), goma);
    }

    [Fact]
    public void A_local_time_inside_the_spring_forward_gap_is_nudged_rather_than_thrown()
    {
        // Brussels skips 02:00-03:00 on 29 March 2026. ConvertTimeToUtc throws
        // for a time in that gap; the seeder would die on one date a year.
        var gap = new DateTime(2026, 3, 29, 2, 30, 0);
        Assert.True(Brussels.IsInvalidTime(gap));

        var utc = AirportClock.ToUtc(gap, Brussels);

        // 03:30 CEST is 01:30 UTC.
        Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ToLocal_reverses_ToUtc()
    {
        var local = new DateTime(2026, 9, 7, 15, 45, 0);

        var roundTripped = AirportClock.ToLocal(AirportClock.ToUtc(local, Goma), Goma);

        Assert.Equal(local, roundTripped);
    }

    [Fact]
    public void ToUtc_always_returns_a_UTC_kind()
    {
        // Npgsql rejects a DateTime whose Kind is not Utc when writing to a
        // timestamptz column, so this is a storage requirement, not a nicety.
        var utc = AirportClock.ToUtc(new DateTime(2026, 9, 7, 7, 30, 0), Goma);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void An_unknown_zone_falls_back_to_UTC_instead_of_throwing()
    {
        // Containers built without ICU cannot resolve IANA ids. Falling back
        // keeps the site serving with times an hour or two out, rather than
        // failing every request that touches a flight.
        var zone = AirportClock.Resolve("Not/ARealZone");

        Assert.Equal(TimeZoneInfo.Utc, zone);
    }

    [Theory]
    [InlineData("Africa/Lubumbashi")]
    [InlineData("Africa/Kinshasa")]
    [InlineData("Africa/Kigali")]
    [InlineData("Africa/Kampala")]
    [InlineData("Africa/Bujumbura")]
    [InlineData("Africa/Casablanca")]
    public void Every_seeded_zone_resolves_on_this_platform(string timeZoneId)
    {
        Assert.NotEqual(TimeZoneInfo.Utc, AirportClock.Resolve(timeZoneId));
    }
}
