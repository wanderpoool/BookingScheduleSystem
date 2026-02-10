using BookingScheduleSystem.Contracts.Notifications;

namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

public sealed class InAppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid UserId { get; set; }
    public required Guid TenantId { get; set; }
    public required NotificationType Type { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
