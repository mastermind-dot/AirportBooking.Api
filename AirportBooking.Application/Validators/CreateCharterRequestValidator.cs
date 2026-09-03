using AirportBooking.Application.DTOs.Charters;
using AirportBooking.Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AirportBooking.Application.Validators;

public sealed class CreateCharterRequestValidator : AbstractValidator<CreateCharterRequest>
{
    /// <summary>The G159 is the larger cabin at 24 seats; beyond that it is two aircraft.</summary>
    private const int MaxPassengers = 30;

    /// <summary>The SD360 lifts 3 500 kg. Anything heavier is multiple rotations.</summary>
    private const decimal MaxCargoKg = 3500;

    public CreateCharterRequestValidator(IStringLocalizer<ValidationMessages> text)
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.PreferredAircraft).IsInEnum();

        RuleFor(x => x.ContactName)
            .NotEmpty().WithMessage(_ => text["Charter.NameRequired"])
            .MaximumLength(128);

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage(_ => text["Required.Email"])
            .EmailAddress().WithMessage(_ => text["Invalid.Email"])
            .MaximumLength(256);

        RuleFor(x => x.ContactPhone)
            .MaximumLength(32)
            .Matches(@"^[\d\s+()\-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone))
            .WithMessage(_ => text["Invalid.Phone"]);

        RuleFor(x => x.Company).MaximumLength(128);

        RuleFor(x => x.Origin)
            .NotEmpty().WithMessage(_ => text["Charter.OriginRequired"])
            .MaximumLength(128);

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage(_ => text["Charter.DestinationRequired"])
            .MaximumLength(128);

        RuleFor(x => x.DepartureDate)
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(_ => text["Charter.DateInPast"])
            // A charter needs permits, crew and fuel arranged; an enquiry three
            // years out is a typo, not a booking.
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)))
            .WithMessage(_ => text["Charter.DateTooFar"]);

        RuleFor(x => x.ReturnDate)
            .GreaterThanOrEqualTo(x => x.DepartureDate)
            .When(x => x.ReturnDate.HasValue)
            .WithMessage(_ => text["Charter.ReturnBeforeDeparture"]);

        // The two kinds need different facts, and asking for both would make
        // every enquiry twice as long as it needs to be.
        RuleFor(x => x.PassengerCount)
            .NotNull().WithMessage(_ => text["Charter.PassengersRequired"])
            .InclusiveBetween(1, MaxPassengers)
            .WithMessage(_ => string.Format(text["Charter.PassengerRange"], MaxPassengers))
            .When(x => x.Kind == CharterKind.Passenger);

        RuleFor(x => x.CargoWeightKg)
            .NotNull().WithMessage(_ => text["Charter.WeightRequired"])
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxCargoKg)
            .WithMessage(_ => string.Format(text["Charter.WeightRange"], MaxCargoKg))
            .When(x => x.Kind == CharterKind.Cargo);

        RuleFor(x => x.CargoDescription)
            .NotEmpty().WithMessage(_ => text["Charter.CargoDescription"])
            .MaximumLength(512)
            .When(x => x.Kind == CharterKind.Cargo);

        RuleFor(x => x.Message).MaximumLength(2000);
    }
}
