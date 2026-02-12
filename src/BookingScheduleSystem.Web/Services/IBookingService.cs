using BookingScheduleSystem.Contracts.Bookings;
using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Web.Services;

public interface IBookingService
{
    Task<BookingResponse?> CreateBookingAsync(CreateBookingRequest request);
    Task<ListBookingsResponse?> ListBookingsAsync(BookingStatus? status = null, int page = 1, int pageSize = 20);
    Task<BookingResponse?> GetBookingAsync(Guid bookingId);
    Task<BookingResponse?> CancelBookingAsync(Guid bookingId, string? reason = null);
    Task<BookingResponse?> ApproveBookingAsync(Guid bookingId);
    Task<BookingResponse?> RejectBookingAsync(Guid bookingId, string? reason = null);
    Task<BookingResponse?> UpdateBookingNotesAsync(Guid bookingId, string? notes);
}

public sealed record ListBookingsResponse
{
    public required List<BookingResponse> Bookings { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}
