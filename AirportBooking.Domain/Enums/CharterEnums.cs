namespace AirportBooking.Domain.Enums;

/// <summary>What the customer wants moved.</summary>
public enum CharterKind
{
    Passenger = 0,
    Cargo = 1
}

/// <summary>
/// Which aircraft the customer asks for. "Any" is the common case — most
/// enquirers describe the trip and let operations pick the airframe.
/// </summary>
public enum AircraftPreference
{
    Any = 0,
    ShortSd360 = 1,
    GulfstreamG159 = 2
}

/// <summary>
/// Where an enquiry sits in the commercial follow-up. Nothing here is
/// automated: a person moves it along, and the statuses exist so the team can
/// see what still needs answering.
/// </summary>
public enum CharterRequestStatus
{
    New = 0,
    Contacted = 1,
    Quoted = 2,
    Closed = 3
}
