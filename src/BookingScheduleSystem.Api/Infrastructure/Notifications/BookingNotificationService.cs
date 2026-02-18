using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Notifications;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

public class BookingNotificationService
{
    private readonly ILogger<BookingNotificationService> _logger;

    public BookingNotificationService(ILogger<BookingNotificationService> logger)
    {
        _logger = logger;
    }

    public void NotifyBookingCreated(IDocumentSession session, Booking booking, Schedule schedule)
    {
        var isAutoConfirmed = booking.Status == BookingStatus.Confirmed && schedule.ProviderId.HasValue;

        if (schedule.ProviderId.HasValue)
        {
            var providerNotification = new InAppNotification
            {
                UserId = schedule.ProviderId.Value.Value,
                TenantId = booking.TenantId.Value,
                Type = NotificationType.BookingCreated,
                Title = isAutoConfirmed ? "Booking Auto-Confirmed" : "New Booking Request",
                Message = isAutoConfirmed
                    ? $"A booking for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt} has been automatically confirmed."
                    : $"A new booking request has been made for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt}.",
                RelatedEntityId = booking.Id.Value
            };
            session.Store(providerNotification);

            _logger.LogInformation("=== BOOKING NOTIFICATION ===");
            _logger.LogInformation("To Provider: {ProviderId}", schedule.ProviderId.Value);
            _logger.LogInformation("Type: {NotificationType}", isAutoConfirmed ? "Booking Auto-Confirmed" : "New Booking Request (Pending Approval)");
            _logger.LogInformation("Schedule: {Title} on {StartTime}", schedule.Title, schedule.StartTime);
            _logger.LogInformation("============================");
        }

        // Notify customer of booking status
        var isPending = booking.Status == BookingStatus.Pending;
        var customerNotification = new InAppNotification
        {
            UserId = booking.UserId.Value,
            TenantId = booking.TenantId.Value,
            Type = NotificationType.BookingCreated,
            Title = isPending ? "Booking Pending Approval" : "Booking Confirmed",
            Message = isPending
                ? $"Your booking for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt} is pending provider approval."
                : $"Your booking for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt} has been confirmed!",
            RelatedEntityId = booking.Id.Value
        };
        session.Store(customerNotification);

        _logger.LogInformation("=== BOOKING NOTIFICATION ===");
        _logger.LogInformation("To Customer: {UserId}", booking.UserId);
        _logger.LogInformation("Type: Booking {Status}", isPending ? "Pending" : "Confirmed");
        _logger.LogInformation("Schedule: {Title} on {StartTime}", schedule.Title, schedule.StartTime);
        _logger.LogInformation("============================");
    }

    public void NotifyBookingConfirmed(IDocumentSession session, Booking booking, Schedule schedule)
    {
        var notification = new InAppNotification
        {
            UserId = booking.UserId.Value,
            TenantId = booking.TenantId.Value,
            Type = NotificationType.BookingConfirmed,
            Title = "Booking Confirmed",
            Message = $"Your booking for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt} has been approved!",
            RelatedEntityId = booking.Id.Value
        };
        session.Store(notification);

        _logger.LogInformation("=== BOOKING NOTIFICATION ===");
        _logger.LogInformation("To Customer: {UserId}", booking.UserId);
        _logger.LogInformation("Type: Booking Confirmed");
        _logger.LogInformation("Schedule: {Title} on {StartTime}", schedule.Title, schedule.StartTime);
        _logger.LogInformation("============================");
    }

    public void NotifyBookingRejected(IDocumentSession session, Booking booking, Schedule schedule, string? reason)
    {
        var message = $"Your booking for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt} has been declined.";
        if (!string.IsNullOrWhiteSpace(reason))
            message += $" Reason: {reason}";

        var notification = new InAppNotification
        {
            UserId = booking.UserId.Value,
            TenantId = booking.TenantId.Value,
            Type = NotificationType.BookingRejected,
            Title = "Booking Declined",
            Message = message,
            RelatedEntityId = booking.Id.Value
        };
        session.Store(notification);

        _logger.LogInformation("=== BOOKING NOTIFICATION ===");
        _logger.LogInformation("To Customer: {UserId}", booking.UserId);
        _logger.LogInformation("Type: Booking Rejected");
        _logger.LogInformation("Schedule: {Title}", schedule.Title);
        _logger.LogInformation("Reason: {Reason}", reason ?? "No reason provided");
        _logger.LogInformation("============================");
    }

    public void NotifyBookingCancelled(IDocumentSession session, Booking booking, Schedule schedule)
    {
        // Notify provider if one exists
        if (schedule.ProviderId.HasValue)
        {
            var providerNotification = new InAppNotification
            {
                UserId = schedule.ProviderId.Value.Value,
                TenantId = booking.TenantId.Value,
                Type = NotificationType.BookingCancelled,
                Title = "Booking Cancelled",
                Message = $"A booking for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt} has been cancelled.",
                RelatedEntityId = booking.Id.Value
            };
            session.Store(providerNotification);

            _logger.LogInformation("=== BOOKING NOTIFICATION ===");
            _logger.LogInformation("To Provider: {ProviderId}", schedule.ProviderId.Value);
            _logger.LogInformation("Type: Booking Cancelled");
            _logger.LogInformation("Schedule: {Title} on {StartTime}", schedule.Title, schedule.StartTime);
            _logger.LogInformation("============================");
        }
    }
}
