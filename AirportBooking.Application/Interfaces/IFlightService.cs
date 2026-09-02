using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Flights;

namespace AirportBooking.Application.Interfaces;

public interface IFlightService
{
    Task<Result<PagedResult<FlightSummaryDto>>> SearchAsync(
        FlightSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<FlightDetailsDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Reference data for the origin and destination pickers.</summary>
    Task<IReadOnlyList<AirportDto>> GetAirportsAsync(CancellationToken cancellationToken = default);
}
