using AirportBooking.Domain.Enums;

namespace AirportBooking.Application.DTOs.Bookings;

/// <summary>
/// What the client sends to create a booking.
///
/// Note what is absent: any notion of price. The total is computed server-side
/// from the flight's stored fare, so a tampered request can change what is
/// booked but never what it costs.
/// </summary>
public sealed record CreateBookingRequest(
    Guid FlightId,
    CabinClass Cabin,
    string ContactEmail,
    string? ContactPhone,
    IReadOnlyList<PassengerRequest> Passengers);

public sealed record PassengerRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Nationality,
    string PassportNumber,
    DateOnly? PassportExpiry);

public sealed record PassengerDto(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Nationality,
    string PassportNumber,
    DateOnly? PassportExpiry,
    string? SeatNumber);

/// <summary>The flight as it appears inside a booking — enough to render the itinerary.</summary>
public sealed record BookingFlightDto(
    Guid Id,
    string FlightNumber,
    string AirlineName,
    string OriginIata,
    string OriginCity,
    string DestinationIata,
    string DestinationCity,
    DateTime DepartureTimeUtc,
    DateTime DepartureTimeLocal,
    DateTime ArrivalTimeUtc,
    DateTime ArrivalTimeLocal,
    int DurationMinutes,
    int Stops);

public sealed record BookingDto(
    Guid Id,
    string Reference,
    BookingStatus Status,
    CabinClass Cabin,
    decimal TotalAmount,
    string Currency,
    string ContactEmail,
    string? ContactPhone,
    DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? CancelledAtUtc,
    BookingFlightDto Flight,
    IReadOnlyList<PassengerDto> Passengers,
    PaymentStatus? PaymentStatus);

/// <summary>The row shape for the bookings list — no passenger detail.</summary>
public sealed record BookingSummaryDto(
    Guid Id,
    string Reference,
    BookingStatus Status,
    CabinClass Cabin,
    decimal TotalAmount,
    string Currency,
    int PassengerCount,
    DateTime CreatedAtUtc,
    BookingFlightDto Flight);
