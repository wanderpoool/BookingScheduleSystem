namespace BookingScheduleSystem.Contracts.Notifications;

public enum NotificationType
{
    BookingCreated,
    BookingConfirmed,
    BookingCancelled,
    BookingRejected,
    BookingReminder
}

public sealed record NotificationResponse
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required NotificationType Type { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public Guid? RelatedEntityId { get; init; }
    public required bool IsRead { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record ListNotificationsResponse
{
    public required List<NotificationResponse> Notifications { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}

public sealed record UnreadCountResponse
{
    public required int Count { get; init; }
}
