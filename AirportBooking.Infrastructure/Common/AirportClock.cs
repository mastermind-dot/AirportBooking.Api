using Microsoft.Extensions.Logging;

namespace AirportBooking.Infrastructure.Common;

/// <summary>
/// Converts between an airport's local wall-clock time and the UTC instants
/// stored in the database.
///
/// Shared by the seeder and by search so there is one implementation of the
/// awkward part: on the spring-forward date an hour of local time does not
/// exist, and asking to convert 02:30 that day throws.
/// </summary>
internal static class AirportClock
{
    /// <summary>
    /// Resolves an IANA identifier, falling back to UTC with a warning. Images
    /// built without ICU — or with InvariantGlobalization enabled — cannot
    /// resolve these, and the raw exception is hard to trace back to a cause.
    /// </summary>
    public static TimeZoneInfo Resolve(string timeZoneId, ILogger? logger = null)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger?.LogWarning("Time zone {TimeZoneId} could not be resolved; falling back to UTC.", timeZoneId);
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>Local wall-clock time at an airport to the UTC instant it denotes.</summary>
    public static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        // The clock skips an hour when DST begins, so times inside the gap never
        // occur. Nudging forward keeps the caller's intent — a departure "at
        // 02:30" becomes 03:30 — rather than throwing.
        if (timeZone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            timeZone);
    }

    /// <summary>A stored UTC instant back to what a traveller reads at that airport.</summary>
    public static DateTime ToLocal(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
}
