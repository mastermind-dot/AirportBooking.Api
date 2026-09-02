namespace AirportBooking.Infrastructure.Data.Seed;

/// <summary>
/// Reference airports. Real IATA codes and real IANA time zones, because the
/// search results show local departure times — seeding "Europe/Brussels" as
/// something invented would break every displayed time.
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
        // Belgium
        new("BRU", "Brussels Airport",                  "Brussels",   "BE", "Europe/Brussels"),
        new("CRL", "Brussels South Charleroi Airport",  "Charleroi",  "BE", "Europe/Brussels"),

        // Western Europe
        new("AMS", "Amsterdam Airport Schiphol",        "Amsterdam",  "NL", "Europe/Amsterdam"),
        new("CDG", "Paris Charles de Gaulle Airport",   "Paris",      "FR", "Europe/Paris"),
        new("ORY", "Paris Orly Airport",                "Paris",      "FR", "Europe/Paris"),
        new("LHR", "London Heathrow Airport",           "London",     "GB", "Europe/London"),
        new("DUB", "Dublin Airport",                    "Dublin",     "IE", "Europe/Dublin"),
        new("LUX", "Luxembourg Findel Airport",         "Luxembourg", "LU", "Europe/Luxembourg"),

        // Central Europe
        new("FRA", "Frankfurt Airport",                 "Frankfurt",  "DE", "Europe/Berlin"),
        new("MUC", "Munich Airport",                    "Munich",     "DE", "Europe/Berlin"),
        new("ZRH", "Zurich Airport",                    "Zurich",     "CH", "Europe/Zurich"),
        new("VIE", "Vienna International Airport",      "Vienna",     "AT", "Europe/Vienna"),

        // Southern Europe
        new("MAD", "Adolfo Suarez Madrid-Barajas",      "Madrid",     "ES", "Europe/Madrid"),
        new("BCN", "Josep Tarradellas Barcelona-El Prat", "Barcelona", "ES", "Europe/Madrid"),
        new("FCO", "Rome Fiumicino Airport",            "Rome",       "IT", "Europe/Rome"),
        new("MXP", "Milan Malpensa Airport",            "Milan",      "IT", "Europe/Rome"),
        new("LIS", "Humberto Delgado Airport",          "Lisbon",     "PT", "Europe/Lisbon"),
        new("ATH", "Athens International Airport",      "Athens",     "GR", "Europe/Athens"),

        // Northern Europe
        new("CPH", "Copenhagen Airport",                "Copenhagen", "DK", "Europe/Copenhagen"),
        new("ARN", "Stockholm Arlanda Airport",         "Stockholm",  "SE", "Europe/Stockholm"),
        new("OSL", "Oslo Gardermoen Airport",           "Oslo",       "NO", "Europe/Oslo"),

        // Beyond Europe
        new("IST", "Istanbul Airport",                  "Istanbul",   "TR", "Europe/Istanbul"),
        new("DXB", "Dubai International Airport",       "Dubai",      "AE", "Asia/Dubai"),
        new("JFK", "John F. Kennedy International",     "New York",   "US", "America/New_York"),
        new("YUL", "Montreal-Trudeau International",    "Montreal",   "CA", "America/Toronto"),
        new("CMN", "Mohammed V International Airport",  "Casablanca", "MA", "Africa/Casablanca"),
        new("RAK", "Marrakesh Menara Airport",          "Marrakesh",  "MA", "Africa/Casablanca")
    ];
}
