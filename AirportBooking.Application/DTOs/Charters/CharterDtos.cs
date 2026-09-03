using AirportBooking.Domain.Enums;

namespace AirportBooking.Application.DTOs.Charters;

/// <summary>
/// A charter enquiry. Origin and destination are free text on purpose: the
/// strips worth chartering to often have no IATA code, and a dropdown of known
/// airports would turn away exactly the business the SD360 exists for.
/// </summary>
public sealed record CreateCharterRequest(
    CharterKind Kind,
    string ContactName,
    string ContactEmail,
    string? ContactPhone,
    string? Company,
    string Origin,
    string Destination,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    AircraftPreference PreferredAircraft,
    int? PassengerCount,
    decimal? CargoWeightKg,
    string? CargoDescription,
    string? Message);

/// <summary>
/// The acknowledgement. Only the reference and the essentials come back — the
/// customer already knows what they typed, and echoing it adds nothing.
/// </summary>
public sealed record CharterRequestDto(
    Guid Id,
    string Reference,
    CharterKind Kind,
    CharterRequestStatus Status,
    string Origin,
    string Destination,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    DateTime CreatedAtUtc);
