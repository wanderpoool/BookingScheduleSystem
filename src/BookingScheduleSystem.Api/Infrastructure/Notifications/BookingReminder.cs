using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

/// <summary>
/// Tracks which bookings have already received reminder notifications
/// to prevent duplicate sends across job runs.
/// </summary>
public sealed class BookingReminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required BookingId BookingId { get; set; }
    public required ScheduleId ScheduleId { get; set; }
    public required TenantId TenantId { get; set; }
    public required UserId CustomerUserId { get; set; }
    public UserId? ProviderUserId { get; set; }
    public required DateTime ScheduleStartTime { get; set; }
    public bool CustomerNotified { get; set; }
    public bool ProviderNotified { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
