namespace AirportBooking.Infrastructure.Data.Seed;

/// <summary>
/// The airports Malu Aviation actually works with: the two company bases, the
/// eastern DRC network the Goma SD360s serve, the larger domestic fields the
/// G159 reaches on charter, and the three regional capitals next door.
///
/// The DRC spans two time zones. Kinshasa, Kongo-Central and Équateur keep
/// UTC+1 (Africa/Kinshasa); the Kivus, Ituri, Tshopo, Maniema, the Kasaïs and
/// Katanga keep UTC+2 (Africa/Lubumbashi). Getting this wrong would show every
/// eastern departure an hour out.
/// </summary>
internal static class AirportSeedData
{
    internal sealed record AirportDefinition(
        string Iata,
        string Name,
        string City,
        string CountryCode,
        string TimeZoneId);

    internal static readonly AirportDefinition[] Airports =
    [
        // Company bases
        new("GOM", "Goma International Airport",        "Goma",        "CD", "Africa/Lubumbashi"),
        new("NLO", "Ndolo Airport",                     "Kinshasa",    "CD", "Africa/Kinshasa"),
        new("FIH", "N'djili International Airport",     "Kinshasa",    "CD", "Africa/Kinshasa"),

        // Eastern DRC — the scheduled SD360 network out of Goma
        new("BKY", "Kavumu Airport",                    "Bukavu",      "CD", "Africa/Lubumbashi"),
        new("BNC", "Mavivi Airport",                    "Beni",        "CD", "Africa/Lubumbashi"),
        new("RUE", "Rughenda Airfield",                 "Butembo",     "CD", "Africa/Lubumbashi"),
        new("BUX", "Bunia Airport",                     "Bunia",       "CD", "Africa/Lubumbashi"),
        new("FKI", "Bangoka International Airport",     "Kisangani",   "CD", "Africa/Lubumbashi"),
        new("KND", "Kindu Airport",                     "Kindu",       "CD", "Africa/Lubumbashi"),

        // Wider domestic network — charter territory for the G159
        new("FBM", "Lubumbashi International Airport",  "Lubumbashi",  "CD", "Africa/Lubumbashi"),
        new("MJM", "Mbuji-Mayi Airport",                "Mbuji-Mayi",  "CD", "Africa/Lubumbashi"),
        new("KGA", "Kananga Airport",                   "Kananga",     "CD", "Africa/Lubumbashi"),
        new("FMI", "Kalemie Airport",                   "Kalemie",     "CD", "Africa/Lubumbashi"),
        new("MDK", "Mbandaka Airport",                  "Mbandaka",    "CD", "Africa/Kinshasa"),
        new("MAT", "Tshimpi Airport",                   "Matadi",      "CD", "Africa/Kinshasa"),

        // Regional neighbours
        new("KGL", "Kigali International Airport",      "Kigali",      "RW", "Africa/Kigali"),
        new("EBB", "Entebbe International Airport",     "Entebbe",     "UG", "Africa/Kampala"),
        new("BJM", "Bujumbura International Airport",   "Bujumbura",   "BI", "Africa/Bujumbura")
    ];
}
