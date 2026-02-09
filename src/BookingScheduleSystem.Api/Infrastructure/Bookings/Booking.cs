using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.Bookings;

public sealed class Booking
{
    public BookingId Id { get; set; }
    public required ScheduleId ScheduleId { get; set; }
    public required UserId UserId { get; set; }
    public required TenantId TenantId { get; set; }
    public BookingStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime BookedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
