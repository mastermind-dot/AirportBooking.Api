using AirportBooking.Application.DTOs.Charters;
using AirportBooking.Domain.Enums;
using FluentValidation;

namespace AirportBooking.Application.Validators;

public sealed class CreateCharterRequestValidator : AbstractValidator<CreateCharterRequest>
{
    /// <summary>The G159 is the larger cabin at 24 seats; beyond that it is two aircraft.</summary>
    private const int MaxPassengers = 30;

    /// <summary>The SD360 lifts 3 500 kg. Anything heavier is multiple rotations.</summary>
    private const decimal MaxCargoKg = 3500;

    public CreateCharterRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.PreferredAircraft).IsInEnum();

        RuleFor(x => x.ContactName)
            .NotEmpty().WithMessage("A contact name is required.")
            .MaximumLength(128);

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage("An email address is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256);

        RuleFor(x => x.ContactPhone)
            .MaximumLength(32)
            .Matches(@"^[\d\s+()\-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone))
            .WithMessage("Enter a valid phone number.");

        RuleFor(x => x.Company).MaximumLength(128);

        RuleFor(x => x.Origin)
            .NotEmpty().WithMessage("Where does the flight depart from?")
            .MaximumLength(128);

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("Where is the flight going?")
            .MaximumLength(128);

        RuleFor(x => x.DepartureDate)
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("The departure date cannot be in the past.")
            // A charter needs permits, crew and fuel arranged; an enquiry three
            // years out is a typo, not a booking.
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)))
            .WithMessage("Please enquire closer to the date.");

        RuleFor(x => x.ReturnDate)
            .GreaterThanOrEqualTo(x => x.DepartureDate)
            .When(x => x.ReturnDate.HasValue)
            .WithMessage("The return date cannot be before the departure date.");

        // The two kinds need different facts, and asking for both would make
        // every enquiry twice as long as it needs to be.
        RuleFor(x => x.PassengerCount)
            .NotNull().WithMessage("How many passengers?")
            .InclusiveBetween(1, MaxPassengers)
            .WithMessage($"Between 1 and {MaxPassengers} passengers per charter.")
            .When(x => x.Kind == CharterKind.Passenger);

        RuleFor(x => x.CargoWeightKg)
            .NotNull().WithMessage("How much does the cargo weigh?")
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxCargoKg)
            .WithMessage($"Up to {MaxCargoKg:N0} kg per rotation — tell us in the message if it is more.")
            .When(x => x.Kind == CharterKind.Cargo);

        RuleFor(x => x.CargoDescription)
            .NotEmpty().WithMessage("Tell us what the cargo is.")
            .MaximumLength(512)
            .When(x => x.Kind == CharterKind.Cargo);

        RuleFor(x => x.Message).MaximumLength(2000);
    }
}
