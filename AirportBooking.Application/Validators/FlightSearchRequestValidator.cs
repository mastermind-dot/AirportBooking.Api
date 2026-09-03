using AirportBooking.Application.DTOs.Flights;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AirportBooking.Application.Validators;

public sealed class FlightSearchRequestValidator : AbstractValidator<FlightSearchRequest>
{
    /// <summary>
    /// Caps the page size so a caller cannot ask for the entire table in one
    /// request and turn the search endpoint into a denial-of-service lever.
    /// </summary>
    public const int MaxPageSize = 100;

    public FlightSearchRequestValidator(IStringLocalizer<ValidationMessages> text)
    {
        RuleFor(x => x.Origin)
            .NotEmpty().WithMessage(_ => text["Search.OriginRequired"])
            .Length(3).WithMessage(_ => text["Search.IataFormat"]);

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage(_ => text["Search.DestinationRequired"])
            .Length(3).WithMessage(_ => text["Search.IataFormat"]);

        RuleFor(x => x.DepartureDate)
            .NotEmpty().WithMessage(_ => text["Search.DateRequired"])
            // Compared in the origin's local calendar terms; a flight earlier
            // today is filtered out by the query, which knows the time zone.
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
            .WithMessage(_ => text["Search.DateInPast"]);

        RuleFor(x => x.Passengers)
            .InclusiveBetween(1, 9)
            .WithMessage(_ => text["Search.PassengerRange"]);

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
            .WithMessage(_ => text["Search.MaxPriceBelowMin"]);

        RuleFor(x => x)
            .Must(x => x.DepartAfter < x.DepartBefore)
            .When(x => x.DepartAfter.HasValue && x.DepartBefore.HasValue)
            .WithName("DepartBefore")
            .WithMessage(_ => text["Search.TimeWindow"]);

        RuleFor(x => x.MaxStops)
            .InclusiveBetween(0, 3).When(x => x.MaxStops.HasValue);

        RuleForEach(x => x.Airlines)
            .Length(2, 3).WithMessage(_ => text["Search.AirlineCode"])
            .When(x => x.Airlines is { Length: > 0 });

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(_ => text["Search.PageMin"]);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage(_ => string.Format(text["Search.PageSizeRange"], MaxPageSize));
    }
}
