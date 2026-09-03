using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Charters;
using AirportBooking.Application.Interfaces;
using AirportBooking.Domain.Entities;
using AirportBooking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AirportBooking.Infrastructure.Charters;

public sealed class CharterService : ICharterService
{
    private static readonly string[] SupportedLocales = ["fr", "en"];

    private readonly AppDbContext _db;
    private readonly ILogger<CharterService> _logger;

    public CharterService(AppDbContext db, ILogger<CharterService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<CharterRequestDto>> SubmitAsync(
        CreateCharterRequest request,
        Guid? userId,
        string locale,
        CancellationToken cancellationToken = default)
    {
        // The header is attacker-controlled and only decides which language the
        // team replies in, so an unexpected value falls back rather than being
        // stored and echoed later.
        var normalised = SupportedLocales.Contains(locale) ? locale : "fr";

        var charter = new CharterRequest(
            request.Kind,
            request.ContactName.Trim(),
            request.ContactEmail.Trim(),
            string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim(),
            string.IsNullOrWhiteSpace(request.Company) ? null : request.Company.Trim(),
            request.Origin.Trim(),
            request.Destination.Trim(),
            request.DepartureDate,
            request.ReturnDate,
            request.PreferredAircraft,
            request.PassengerCount,
            request.CargoWeightKg,
            string.IsNullOrWhiteSpace(request.CargoDescription) ? null : request.CargoDescription.Trim(),
            string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            normalised,
            userId);

        _db.CharterRequests.Add(charter);
        await _db.SaveChangesAsync(cancellationToken);

        // Logged without the contact details: an enquiry is personal data, and
        // the reference is enough to find it when someone asks.
        _logger.LogInformation(
            "Charter enquiry {Reference} received ({Kind}, {Origin} to {Destination}, {Date}).",
            charter.Reference, charter.Kind, charter.Origin, charter.Destination, charter.DepartureDate);

        return Result<CharterRequestDto>.Success(ToDto(charter));
    }

    public async Task<IReadOnlyList<CharterRequestDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.CharterRequests
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .Select(r => new CharterRequestDto(
                r.Id,
                r.Reference,
                r.Kind,
                r.Status,
                r.Origin,
                r.Destination,
                r.DepartureDate,
                r.ReturnDate,
                r.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static CharterRequestDto ToDto(CharterRequest r) =>
        new(r.Id, r.Reference, r.Kind, r.Status, r.Origin, r.Destination,
            r.DepartureDate, r.ReturnDate, r.CreatedAtUtc);
}
