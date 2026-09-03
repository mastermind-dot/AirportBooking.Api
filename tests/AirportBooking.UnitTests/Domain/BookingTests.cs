using AirportBooking.Domain.Entities;
using AirportBooking.Domain.Enums;
using AirportBooking.Domain.Exceptions;

namespace AirportBooking.UnitTests.Domain;

/// <summary>
/// Booking's status transitions are the rules that decide whether a seat is
/// sold. They matter most under redelivery: Stripe sends every webhook at
/// least once, so each transition has to be safe to apply twice.
/// </summary>
public class BookingTests
{
    private static Booking Pending(decimal total = 308m) =>
        new(
            userId: Guid.NewGuid(),
            flightId: Guid.NewGuid(),
            cabinClass: CabinClass.Economy,
            totalAmount: total,
            currency: "USD",
            contactEmail: "passager@example.cd");

    private static Passenger Traveller(string passport = "CD1234567") => new()
    {
        FirstName = "Marie",
        LastName = "Kalonji",
        DateOfBirth = new DateOnly(1990, 4, 12),
        Nationality = "CD",
        PassportNumber = passport
    };

    // ---------- creation ----------

    [Fact]
    public void A_new_booking_is_pending_and_unpaid()
    {
        var booking = Pending();

        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Null(booking.ConfirmedAtUtc);
        Assert.Null(booking.CancelledAtUtc);
        Assert.Null(booking.Payment);
    }

    [Fact]
    public void A_booking_needs_a_positive_total()
    {
        Assert.Throws<DomainException>(() => Pending(total: 0m));
    }

    [Fact]
    public void The_reference_is_prefixed_and_readable_over_the_phone()
    {
        var booking = Pending();

        Assert.StartsWith("MLU-", booking.Reference);
        Assert.Equal(10, booking.Reference.Length);

        // Ambiguous glyphs are excluded so a reference read aloud or off a
        // screen cannot be mistyped as a different valid one.
        var body = booking.Reference["MLU-".Length..];
        Assert.DoesNotContain(body, c => c is '0' or 'O' or '1' or 'I');
    }

    [Fact]
    public void References_do_not_collide_across_many_bookings()
    {
        var references = Enumerable.Range(0, 500).Select(_ => Pending().Reference).ToList();

        Assert.Equal(references.Count, references.Distinct().Count());
    }

    // ---------- passengers ----------

    [Fact]
    public void PassengerCount_follows_the_passengers_added()
    {
        var booking = Pending();

        booking.AddPassenger(Traveller("CD1111111"));
        booking.AddPassenger(Traveller("CD2222222"));

        Assert.Equal(2, booking.PassengerCount);
    }

    [Fact]
    public void Passengers_cannot_be_added_once_the_booking_is_settled()
    {
        var booking = Pending();
        booking.AddPassenger(Traveller());
        booking.MarkConfirmed();

        Assert.Throws<DomainException>(() => booking.AddPassenger(Traveller("CD9999999")));
    }

    // ---------- confirmation ----------

    [Fact]
    public void MarkConfirmed_records_when_it_happened()
    {
        var booking = Pending();

        booking.MarkConfirmed();

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ConfirmedAtUtc);
    }

    [Fact]
    public void MarkConfirmed_is_idempotent_because_webhooks_are_redelivered()
    {
        var booking = Pending();
        booking.MarkConfirmed();
        var firstConfirmedAt = booking.ConfirmedAtUtc;

        booking.MarkConfirmed();

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(firstConfirmedAt, booking.ConfirmedAtUtc);
    }

    [Fact]
    public void A_cancelled_booking_cannot_be_confirmed()
    {
        // A late webhook for a booking the customer already cancelled must not
        // resurrect it and sell the seat twice.
        var booking = Pending();
        booking.Cancel();

        Assert.Throws<DomainException>(booking.MarkConfirmed);
    }

    // ---------- failure and cancellation ----------

    [Fact]
    public void MarkFailed_moves_a_pending_booking_to_failed()
    {
        var booking = Pending();

        booking.MarkFailed();

        Assert.Equal(BookingStatus.Failed, booking.Status);
    }

    [Fact]
    public void A_confirmed_booking_cannot_be_marked_failed()
    {
        var booking = Pending();
        booking.MarkConfirmed();

        Assert.Throws<DomainException>(booking.MarkFailed);
    }

    [Fact]
    public void Cancel_records_when_it_happened()
    {
        var booking = Pending();

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.NotNull(booking.CancelledAtUtc);
    }

    [Fact]
    public void Cancelling_twice_is_harmless_and_keeps_the_first_timestamp()
    {
        var booking = Pending();
        booking.Cancel();
        var firstCancelledAt = booking.CancelledAtUtc;

        booking.Cancel();

        Assert.Equal(firstCancelledAt, booking.CancelledAtUtc);
    }

    [Fact]
    public void A_failed_booking_cannot_be_cancelled()
    {
        var booking = Pending();
        booking.MarkFailed();

        Assert.Throws<DomainException>(booking.Cancel);
    }
}
