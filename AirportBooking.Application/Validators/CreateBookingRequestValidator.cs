using AirportBooking.Application.DTOs.Bookings;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AirportBooking.Application.Validators;

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public const int MaxPassengersPerBooking = 9;

    public CreateBookingRequestValidator(IStringLocalizer<ValidationMessages> text)
    {
        RuleFor(x => x.FlightId)
            .NotEmpty().WithMessage(_ => text["Booking.FlightRequired"]);

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage(_ => text["Booking.ContactEmail"])
            .EmailAddress().WithMessage(_ => text["Invalid.Email"])
            .MaximumLength(256);

        RuleFor(x => x.ContactPhone)
            .MaximumLength(32)
            .Matches(@"^[\d\s+()\-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone))
            .WithMessage(_ => text["Invalid.Phone"]);

        RuleFor(x => x.Passengers)
            .NotEmpty().WithMessage(_ => text["Booking.PassengersRequired"])
            .Must(p => p.Count <= MaxPassengersPerBooking)
            .WithMessage(_ => string.Format(text["Booking.TooManyPassengers"], MaxPassengersPerBooking));

        RuleForEach(x => x.Passengers).SetValidator(new PassengerRequestValidator(text));

        // Two passengers with the same passport is a duplicated form row, not a
        // real booking — and it would consume a seat that cannot be flown.
        RuleFor(x => x.Passengers)
            .Must(passengers => passengers
                .Select(p => p.PassportNumber?.Trim().ToUpperInvariant())
                .Distinct()
                .Count() == passengers.Count)
            .When(x => x.Passengers is { Count: > 1 })
            .WithMessage(_ => text["Booking.DuplicatePassport"]);
    }
}

public sealed class PassengerRequestValidator : AbstractValidator<PassengerRequest>
{
    public PassengerRequestValidator(IStringLocalizer<ValidationMessages> text)
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(_ => text["Required.FirstName"])
            .MaximumLength(64);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(_ => text["Required.LastName"])
            .MaximumLength(64);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage(_ => text["Passenger.DobRequired"])
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(_ => text["Passenger.DobInFuture"])
            .GreaterThan(new DateOnly(1900, 1, 1))
            .WithMessage(_ => text["Passenger.DobImplausible"]);

        // The pattern already pins the length at two, so a separate Length rule
        // would fail alongside it and show the user the same sentence twice.
        RuleFor(x => x.Nationality)
            .NotEmpty().WithMessage(_ => text["Passenger.NationalityRequired"])
            .Matches("^[A-Za-z]{2}$").WithMessage(_ => text["Passenger.NationalityFormat"]);

        RuleFor(x => x.PassportNumber)
            .NotEmpty().WithMessage(_ => text["Passenger.PassportRequired"])
            .MaximumLength(20)
            .Matches("^[A-Za-z0-9]+$").WithMessage(_ => text["Passenger.PassportFormat"]);

        // Most carriers require six months' validity beyond travel. This checks
        // only that it has not already expired — the airline's own rule is
        // stricter and belongs with the airline.
        RuleFor(x => x.PassportExpiry)
            .GreaterThan(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.PassportExpiry.HasValue)
            .WithMessage(_ => text["Passenger.PassportExpired"]);
    }
}
