using AirportBooking.Application.DTOs.Bookings;
using FluentValidation;

namespace AirportBooking.Application.Validators;

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public const int MaxPassengersPerBooking = 9;

    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.FlightId)
            .NotEmpty().WithMessage("A flight must be selected.");

        RuleFor(x => x.Cabin).IsInEnum();

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage("A contact email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256);

        RuleFor(x => x.ContactPhone)
            .MaximumLength(32)
            .Matches(@"^[\d\s+()\-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone))
            .WithMessage("Enter a valid phone number.");

        RuleFor(x => x.Passengers)
            .NotEmpty().WithMessage("At least one passenger is required.")
            .Must(p => p.Count <= MaxPassengersPerBooking)
            .WithMessage($"A booking can hold at most {MaxPassengersPerBooking} passengers.");

        RuleForEach(x => x.Passengers).SetValidator(new PassengerRequestValidator());

        // Two passengers with the same passport is a duplicated form row, not a
        // real booking — and it would consume a seat that cannot be flown.
        RuleFor(x => x.Passengers)
            .Must(passengers => passengers
                .Select(p => p.PassportNumber?.Trim().ToUpperInvariant())
                .Distinct()
                .Count() == passengers.Count)
            .When(x => x.Passengers is { Count: > 1 })
            .WithMessage("Each passenger needs a different passport number.");
    }
}

public sealed class PassengerRequestValidator : AbstractValidator<PassengerRequest>
{
    public PassengerRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(64);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(64);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.")
            .GreaterThan(new DateOnly(1900, 1, 1))
            .WithMessage("Enter a valid date of birth.");

        // The pattern already pins the length at two, so a separate Length rule
        // would fail alongside it and show the user the same sentence twice.
        RuleFor(x => x.Nationality)
            .NotEmpty().WithMessage("Nationality is required.")
            .Matches("^[A-Za-z]{2}$").WithMessage("Use a two-letter country code, e.g. BE.");

        RuleFor(x => x.PassportNumber)
            .NotEmpty().WithMessage("Passport number is required.")
            .MaximumLength(20)
            .Matches("^[A-Za-z0-9]+$").WithMessage("Passport numbers are letters and digits only.");

        // Most carriers require six months' validity beyond travel. This checks
        // only that it has not already expired — the airline's own rule is
        // stricter and belongs with the airline.
        RuleFor(x => x.PassportExpiry)
            .GreaterThan(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.PassportExpiry.HasValue)
            .WithMessage("That passport has expired.");
    }
}
