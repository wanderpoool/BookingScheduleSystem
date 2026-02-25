using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Notifications;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

public class BookingNotificationService
{
    private readonly ILogger<BookingNotificationService> _logger;
    private readonly ISmsService _smsService;
    private readonly IDocumentStore _store;

    public BookingNotificationService(
        ILogger<BookingNotificationService> logger,
        ISmsService smsService,
        IDocumentStore store)
    {
        _logger = logger;
        _smsService = smsService;
        _store = store;
    }

    public void NotifyBookingCreated(IDocumentSession session, Booking booking, Schedule schedule)
    {
        var isAutoConfirmed = booking.Status == BookingStatus.Confirmed && schedule.ProviderId.HasValue;
        var dateFormatted = schedule.StartTime.ToString("MMM dd, yyyy");

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

            // Fire-and-forget SMS to provider
            _ = SendBookingSmsAsync(
                schedule.ProviderId.Value.Value,
                booking.TenantId,
                providerId: null,
                schedule.Title,
                dateFormatted,
                isAutoConfirmed ? BookingSmsType.BookingConfirmedToProvider : BookingSmsType.BookingRequestToProvider);
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

        // Fire-and-forget SMS to customer
        _ = SendBookingSmsAsync(
            booking.UserId.Value,
            booking.TenantId,
            schedule.ProviderId,
            schedule.Title,
            dateFormatted,
            isPending ? BookingSmsType.BookingPendingToCustomer : BookingSmsType.BookingConfirmedToCustomer);
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

        var dateFormatted = schedule.StartTime.ToString("MMM dd, yyyy");
        _ = SendBookingSmsAsync(
            booking.UserId.Value,
            booking.TenantId,
            schedule.ProviderId,
            schedule.Title,
            dateFormatted,
            BookingSmsType.BookingApprovedToCustomer);
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

        var dateFormatted = schedule.StartTime.ToString("MMM dd, yyyy");
        _ = SendBookingSmsAsync(
            booking.UserId.Value,
            booking.TenantId,
            providerId: null,
            schedule.Title,
            dateFormatted,
            BookingSmsType.BookingDeclinedToCustomer);
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

            var dateFormatted = schedule.StartTime.ToString("MMM dd, yyyy");
            _ = SendBookingSmsAsync(
                schedule.ProviderId.Value.Value,
                booking.TenantId,
                providerId: null,
                schedule.Title,
                dateFormatted,
                BookingSmsType.BookingCancelledToProvider);
        }
    }

    private async Task SendBookingSmsAsync(
        Guid recipientUserId,
        TenantId tenantId,
        UserId? providerId,
        string scheduleTitle,
        string dateFormatted,
        BookingSmsType messageType)
    {
        try
        {
            await using var querySession = _store.QuerySession();
            var recipient = await querySession.LoadAsync<User>(new UserId(recipientUserId));

            if (recipient?.PhoneNumber is null or "")
            {
                _logger.LogDebug("No phone number for user {UserId}, skipping booking SMS", recipientUserId);
                return;
            }

            var tenant = await querySession.LoadAsync<Tenant>(tenantId);
            var tenantName = tenant?.Name ?? "";

            string? providerFirstName = null;
            if (providerId.HasValue)
            {
                var provider = await querySession.LoadAsync<User>(providerId.Value);
                providerFirstName = provider?.FirstName;
            }

            var message = ComposeMessage(messageType, scheduleTitle, dateFormatted, tenantName, providerFirstName);
            await _smsService.SendSmsAsync(recipient.PhoneNumber, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send booking SMS to user {UserId}", recipientUserId);
        }
    }

    private static string ComposeMessage(
        BookingSmsType type,
        string title,
        string date,
        string tenantName,
        string? providerFirstName)
    {
        var atTenant = string.IsNullOrEmpty(tenantName) ? "" : $" at {tenantName}";
        var withProvider = string.IsNullOrEmpty(providerFirstName) ? "" : $" with {providerFirstName}";

        return type switch
        {
            BookingSmsType.BookingConfirmedToProvider =>
                $"New booking confirmed for \"{title}\" on {date}{atTenant}.",
            BookingSmsType.BookingRequestToProvider =>
                $"New booking request for \"{title}\" on {date}{atTenant}. Please review.",
            BookingSmsType.BookingPendingToCustomer =>
                $"Your booking for \"{title}\" on {date}{withProvider}{atTenant} is pending approval.",
            BookingSmsType.BookingConfirmedToCustomer =>
                $"Your booking for \"{title}\" on {date}{withProvider}{atTenant} is confirmed!",
            BookingSmsType.BookingApprovedToCustomer =>
                $"Your booking for \"{title}\" on {date}{withProvider}{atTenant} has been approved!",
            BookingSmsType.BookingDeclinedToCustomer =>
                $"Your booking for \"{title}\" on {date}{atTenant} has been declined.",
            BookingSmsType.BookingCancelledToProvider =>
                $"A booking for \"{title}\" on {date}{atTenant} has been cancelled.",
            _ => $"Booking update for \"{title}\" on {date}{atTenant}."
        };
    }

    private enum BookingSmsType
    {
        BookingConfirmedToProvider,
        BookingRequestToProvider,
        BookingPendingToCustomer,
        BookingConfirmedToCustomer,
        BookingApprovedToCustomer,
        BookingDeclinedToCustomer,
        BookingCancelledToProvider
    }
}
