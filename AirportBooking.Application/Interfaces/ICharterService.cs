using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Charters;

namespace AirportBooking.Application.Interfaces;

public interface ICharterService
{
    /// <param name="userId">Null for an anonymous enquiry, which is the common case.</param>
    Task<Result<CharterRequestDto>> SubmitAsync(
        CreateCharterRequest request,
        Guid? userId,
        string locale,
        CancellationToken cancellationToken = default);

    /// <summary>The signed-in customer's own enquiries, so they can follow one up.</summary>
    Task<IReadOnlyList<CharterRequestDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
