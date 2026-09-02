using AirportBooking.Application.DTOs.Flights;
using FluentValidation;

namespace AirportBooking.Application.Validators;

public sealed class FlightSearchRequestValidator : AbstractValidator<FlightSearchRequest>
{
    /// <summary>
    /// Caps the page size so a caller cannot ask for the entire table in one
    /// request and turn the search endpoint into a denial-of-service lever.
    /// </summary>
    public const int MaxPageSize = 100;

    public FlightSearchRequestValidator()
    {
        RuleFor(x => x.Origin)
            .NotEmpty().WithMessage("Origin airport is required.")
            .Length(3).WithMessage("Use a three-letter IATA code, e.g. BRU.");

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("Destination airport is required.")
            .Length(3).WithMessage("Use a three-letter IATA code, e.g. LHR.");

        RuleFor(x => x.DepartureDate)
            .NotEmpty().WithMessage("Departure date is required.")
            // Compared in the origin's local calendar terms; a flight earlier
            // today is filtered out by the query, which knows the time zone.
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
            .WithMessage("Departure date cannot be in the past.");

        RuleFor(x => x.Passengers)
            .InclusiveBetween(1, 9)
            .WithMessage("Between 1 and 9 passengers per booking.");

        RuleFor(x => x.Cabin).IsInEnum();
        RuleFor(x => x.SortBy).IsInEnum();

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0).When(x => x.MaxPrice.HasValue);

        RuleFor(x => x)
            .Must(x => x.MinPrice <= x.MaxPrice)
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue)
            .WithName("MaxPrice")
            .WithMessage("Maximum price must be at least the minimum price.");

        RuleFor(x => x)
            .Must(x => x.DepartAfter < x.DepartBefore)
            .When(x => x.DepartAfter.HasValue && x.DepartBefore.HasValue)
            .WithName("DepartBefore")
            .WithMessage("The latest departure time must be after the earliest.");

        RuleFor(x => x.MaxStops)
            .InclusiveBetween(0, 3).When(x => x.MaxStops.HasValue);

        RuleForEach(x => x.Airlines)
            .Length(2, 3).WithMessage("Airline codes are two or three characters, e.g. SN.")
            .When(x => x.Airlines is { Length: > 0 });

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page numbering starts at 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"Page size must be between 1 and {MaxPageSize}.");
    }
}
