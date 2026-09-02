namespace AirportBooking.Application.Common;

/// <summary>
/// One page of results plus what the client needs to render a pager.
///
/// Search is paginated from the first commit rather than added later: an
/// endpoint that returns everything shapes the UI around that assumption, and
/// retrofitting paging afterwards means changing the contract, the store and
/// every component that reads it.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new([], page, pageSize, 0);
}
