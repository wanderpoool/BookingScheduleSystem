namespace BookingScheduleSystem.Contracts.Bookings;

public sealed record ListBookingsResponse
{
    public required List<BookingResponse> Bookings { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}
