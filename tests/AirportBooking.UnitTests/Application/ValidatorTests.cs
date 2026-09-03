using AirportBooking.Application;
using AirportBooking.Application.DTOs.Bookings;
using AirportBooking.Application.DTOs.Charters;
using AirportBooking.Application.Validators;
using AirportBooking.Domain.Enums;
using Microsoft.Extensions.Localization;

namespace AirportBooking.UnitTests.Application;

/// <summary>
/// Validators are the server-side gate — client validation is UX, and anyone
/// can post past it. These check the rules that protect the booking flow, plus
/// two regressions that reached production behaviour once already.
/// </summary>
public class ValidatorTests
{
    private static readonly IStringLocalizer<ValidationMessages> Text = new PassThroughLocalizer();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // ---------- passengers ----------

    private static PassengerRequest Passenger(
        string first = "Marie",
        string nationality = "CD",
        string passport = "CD1234567",
        DateOnly? dob = null,
        DateOnly? expiry = null) =>
        new(first, "Kalonji", dob ?? new DateOnly(1990, 4, 12), nationality, passport, expiry);

    [Fact]
    public void A_valid_passenger_passes()
    {
        var result = new PassengerRequestValidator(Text).Validate(Passenger());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void A_date_of_birth_in_the_future_is_rejected()
    {
        var result = new PassengerRequestValidator(Text)
            .Validate(Passenger(dob: Today.AddYears(1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PassengerRequest.DateOfBirth));
    }

    [Fact]
    public void An_expired_passport_is_rejected_but_a_missing_expiry_is_allowed()
    {
        var expired = new PassengerRequestValidator(Text)
            .Validate(Passenger(expiry: Today.AddDays(-1)));
        Assert.False(expired.IsValid);

        // Expiry is optional: plenty of domestic DRC travel does not need it.
        var absent = new PassengerRequestValidator(Text).Validate(Passenger(expiry: null));
        Assert.True(absent.IsValid);
    }

    [Fact]
    public void Nationality_reports_one_message_not_two()
    {
        // Regression: Length(2) and the regex both failed for a long value and
        // carried identical text, so the user saw the same sentence twice.
        var result = new PassengerRequestValidator(Text).Validate(Passenger(nationality: "BELGIQUE"));

        var nationalityErrors = result.Errors
            .Where(e => e.PropertyName == nameof(PassengerRequest.Nationality))
            .ToList();

        Assert.Single(nationalityErrors);
    }

    [Theory]
    [InlineData("CD1234567", true)]
    [InlineData("cd1234567", true)]
    [InlineData("CD-123456", false)]
    [InlineData("CD 123456", false)]
    [InlineData("", false)]
    public void Passport_numbers_are_letters_and_digits_only(string passport, bool expectedValid)
    {
        var result = new PassengerRequestValidator(Text).Validate(Passenger(passport: passport));

        Assert.Equal(expectedValid, result.IsValid);
    }

    // ---------- bookings ----------

    private static CreateBookingRequest Booking(params PassengerRequest[] passengers) =>
        new(Guid.NewGuid(), "contact@example.cd", null, passengers);

    [Fact]
    public void A_booking_needs_at_least_one_passenger()
    {
        var result = new CreateBookingRequestValidator(Text).Validate(Booking());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Two_passengers_cannot_share_a_passport_number()
    {
        // A duplicated form row would consume a seat that cannot be flown.
        var result = new CreateBookingRequestValidator(Text).Validate(
            Booking(Passenger(passport: "CD1111111"), Passenger(first: "Joseph", passport: "cd1111111")));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateBookingRequest.Passengers));
    }

    [Fact]
    public void Passenger_errors_are_reported_with_an_indexed_path()
    {
        // The booking form highlights the offending row from this path, so the
        // shape of it is part of the contract.
        var result = new CreateBookingRequestValidator(Text).Validate(
            Booking(Passenger(), Passenger(first: "", passport: "CD2222222")));

        Assert.Contains(result.Errors, e => e.PropertyName == "Passengers[1].FirstName");
    }

    [Fact]
    public void A_booking_cannot_exceed_the_passenger_cap()
    {
        var many = Enumerable.Range(0, CreateBookingRequestValidator.MaxPassengersPerBooking + 1)
            .Select(i => Passenger(passport: $"CD{i:D7}"))
            .ToArray();

        Assert.False(new CreateBookingRequestValidator(Text).Validate(Booking(many)).IsValid);
    }

    // ---------- charter enquiries ----------

    private static CreateCharterRequest Charter(
        CharterKind kind = CharterKind.Passenger,
        int? passengers = 4,
        decimal? weight = null,
        string? cargoDescription = null,
        DateOnly? departure = null,
        DateOnly? returnDate = null) =>
        new(kind, "Marie Kalonji", "marie@example.cd", null, null,
            "Goma", "Piste de Walikale",
            departure ?? Today.AddDays(30), returnDate,
            AircraftPreference.Any, passengers, weight, cargoDescription, null);

    [Fact]
    public void A_passenger_charter_needs_a_passenger_count()
    {
        var result = new CreateCharterRequestValidator(Text).Validate(Charter(passengers: null));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCharterRequest.PassengerCount));
    }

    [Fact]
    public void A_cargo_charter_needs_a_weight_and_a_description()
    {
        var result = new CreateCharterRequestValidator(Text)
            .Validate(Charter(kind: CharterKind.Cargo, passengers: null));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCharterRequest.CargoWeightKg));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCharterRequest.CargoDescription));
    }

    [Fact]
    public void A_cargo_charter_cannot_exceed_what_the_SD360_lifts()
    {
        var result = new CreateCharterRequestValidator(Text).Validate(
            Charter(kind: CharterKind.Cargo, passengers: null, weight: 5000m, cargoDescription: "Ciment"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCharterRequest.CargoWeightKg));
    }

    [Fact]
    public void The_two_charter_kinds_do_not_demand_each_others_fields()
    {
        // A passenger enquiry must not be asked for a cargo weight, and vice
        // versa — otherwise every enquiry is twice as long as it needs to be.
        Assert.True(new CreateCharterRequestValidator(Text).Validate(Charter()).IsValid);

        Assert.True(new CreateCharterRequestValidator(Text).Validate(
            Charter(kind: CharterKind.Cargo, passengers: null, weight: 2400m, cargoDescription: "Forage")).IsValid);
    }

    [Fact]
    public void A_return_date_before_departure_is_rejected()
    {
        var result = new CreateCharterRequestValidator(Text).Validate(
            Charter(departure: Today.AddDays(30), returnDate: Today.AddDays(29)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCharterRequest.ReturnDate));
    }

    [Fact]
    public void A_departure_date_in_the_past_is_rejected()
    {
        var result = new CreateCharterRequestValidator(Text).Validate(
            Charter(departure: Today.AddDays(-1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCharterRequest.DepartureDate));
    }

    /// <summary>
    /// Returns the key as the message. The tests assert on which rule fired,
    /// not on wording — asserting on prose would make every copy edit a test
    /// failure, and the wording is checked against the .resx files instead.
    /// </summary>
    private sealed class PassThroughLocalizer : IStringLocalizer<ValidationMessages>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
