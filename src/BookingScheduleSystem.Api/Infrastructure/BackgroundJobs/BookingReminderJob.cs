using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Notifications;
using Marten;
using Microsoft.Extensions.Options;

namespace BookingScheduleSystem.Api.Infrastructure.BackgroundJobs;

/// <summary>
/// Sends reminder notifications ~1 hour before confirmed appointments.
/// Runs every 30 minutes (configurable). Uses BookingReminder documents
/// to prevent duplicate sends across runs.
/// </summary>
public sealed class BookingReminderJob : PeriodicBackgroundService
{
    private readonly ILogger<BookingReminderJob> _logger;
    private readonly int _leadTimeMinutes;

    public BookingReminderJob(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingReminderJob> logger,
        IOptions<BackgroundJobOptions> options)
        : base(
            scopeFactory,
            logger,
            TimeSpan.FromMinutes(options.Value.BookingReminderIntervalMinutes))
    {
        _logger = logger;
        _leadTimeMinutes = options.Value.ReminderLeadTimeMinutes;
    }

    protected override string JobName => nameof(BookingReminderJob);

    protected override async Task ExecuteJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var smsService = scope.ServiceProvider.GetRequiredService<ISmsService>();
        var emailService = scope.ServiceProvider.GetRequiredService<ReminderEmailService>();

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var windowEnd = now.AddMinutes(_leadTimeMinutes);

        // Find confirmed bookings with schedules starting in [now, now + leadTime]
        var upcomingBookings = await session
            .Query<Booking>()
            .Where(b => b.Status == BookingStatus.Confirmed)
            .ToListAsync(ct);

        if (upcomingBookings.Count == 0)
            return;

        // Load schedules for these bookings and filter by time window
        var scheduleIds = upcomingBookings.Select(b => b.ScheduleId).Distinct().ToList();
        var schedules = new Dictionary<ScheduleId, Schedule>();
        foreach (var scheduleId in scheduleIds)
        {
            var schedule = await session.LoadAsync<Schedule>(scheduleId, ct);
            if (schedule is not null)
                schedules[scheduleId] = schedule;
        }

        // Filter to bookings whose schedule starts within the reminder window
        var eligibleBookings = upcomingBookings
            .Where(b => schedules.TryGetValue(b.ScheduleId, out var s)
                        && s.StartTime >= now
                        && s.StartTime <= windowEnd)
            .ToList();

        if (eligibleBookings.Count == 0)
            return;

        // Check which bookings already have reminders sent
        var eligibleBookingIds = eligibleBookings.Select(b => b.Id).ToList();
        var existingReminders = await session
            .Query<BookingReminder>()
            .ToListAsync(ct);

        var alreadyReminded = existingReminders
            .Where(r => eligibleBookingIds.Contains(r.BookingId))
            .Select(r => r.BookingId)
            .ToHashSet();

        var bookingsToRemind = eligibleBookings
            .Where(b => !alreadyReminded.Contains(b.Id))
            .ToList();

        if (bookingsToRemind.Count == 0)
            return;

        _logger.LogInformation("Found {Count} bookings needing reminders", bookingsToRemind.Count);

        foreach (var booking in bookingsToRemind)
        {
            try
            {
                await SendRemindersForBookingAsync(
                    session, smsService, emailService, booking, schedules[booking.ScheduleId], ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send reminders for booking {BookingId}",
                    booking.Id);
            }
        }

        await session.SaveChangesAsync(ct);
        _logger.LogInformation("Processed {Count} booking reminders", bookingsToRemind.Count);
    }

    private async Task SendRemindersForBookingAsync(
        IDocumentSession session,
        ISmsService smsService,
        ReminderEmailService emailService,
        Booking booking,
        Schedule schedule,
        CancellationToken ct)
    {
        var customer = await session.LoadAsync<User>(booking.UserId, ct);
        if (customer is null)
        {
            _logger.LogWarning("Customer {UserId} not found for booking {BookingId}",
                booking.UserId, booking.Id);
            return;
        }

        User? provider = null;
        if (schedule.ProviderId.HasValue)
            provider = await session.LoadAsync<User>(schedule.ProviderId.Value, ct);

        var tenant = await session.LoadAsync<Tenant>(booking.TenantId, ct);
        var tenantName = tenant?.Name ?? "";
        var dateFormatted = schedule.StartTime.ToString("MMM dd, yyyy 'at' h:mm tt");

        var customerNotified = false;
        var providerNotified = false;

        // --- Customer notifications ---
        customerNotified = await SendNotificationsToUserAsync(
            session, smsService, emailService, customer,
            isCustomer: true,
            booking, schedule, provider, tenantName, dateFormatted, ct);

        // --- Provider notifications ---
        if (provider is not null)
        {
            providerNotified = await SendNotificationsToUserAsync(
                session, smsService, emailService, provider,
                isCustomer: false,
                booking, schedule, customer: customer, tenantName, dateFormatted, ct);
        }

        // Record that reminders were sent
        var reminder = new BookingReminder
        {
            BookingId = booking.Id,
            ScheduleId = schedule.Id,
            TenantId = booking.TenantId,
            CustomerUserId = booking.UserId,
            ProviderUserId = schedule.ProviderId,
            ScheduleStartTime = schedule.StartTime,
            CustomerNotified = customerNotified,
            ProviderNotified = providerNotified
        };
        session.Store(reminder);

        _logger.LogInformation(
            "Reminder sent for booking {BookingId} — customer: {CustomerNotified}, provider: {ProviderNotified}",
            booking.Id, customerNotified, providerNotified);
    }

    private async Task<bool> SendNotificationsToUserAsync(
        IDocumentSession session,
        ISmsService smsService,
        ReminderEmailService emailService,
        User recipient,
        bool isCustomer,
        Booking booking,
        Schedule schedule,
        User? customer,
        string tenantName,
        string dateFormatted,
        CancellationToken ct)
    {
        var notified = false;

        // In-App notification (always)
        try
        {
            var atTenant = string.IsNullOrEmpty(tenantName) ? "" : $" at {tenantName}";
            var title = "Upcoming Appointment";
            var message = isCustomer
                ? $"Reminder: Your appointment for \"{schedule.Title}\" is today at {schedule.StartTime:h:mm tt}{atTenant}."
                : $"Reminder: You have an appointment for \"{schedule.Title}\" today at {schedule.StartTime:h:mm tt}{atTenant}.";

            var notification = new InAppNotification
            {
                UserId = recipient.Id.Value,
                TenantId = booking.TenantId.Value,
                Type = NotificationType.BookingReminder,
                Title = title,
                Message = message,
                RelatedEntityId = booking.Id.Value
            };
            session.Store(notification);
            notified = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create in-app reminder for user {UserId}", recipient.Id);
        }

        // SMS (if phone number exists)
        if (!string.IsNullOrWhiteSpace(recipient.PhoneNumber))
        {
            try
            {
                var atTenant = string.IsNullOrEmpty(tenantName) ? "" : $" at {tenantName}";
                var withPerson = isCustomer
                    ? (schedule.ProviderId.HasValue ? $" with {customer?.FirstName}" : "")
                    : $" with {customer?.FirstName} {customer?.LastName}";
                var suffix = isCustomer
                    ? $"{withPerson} is on {schedule.StartTime:MMM dd} at {schedule.StartTime:h:mm tt}{atTenant}."
                    : $"{withPerson} on {schedule.StartTime:MMM dd} at {schedule.StartTime:h:mm tt}{atTenant}.";
                var prefix = isCustomer
                    ? "Reminder: Your appointment for "
                    : "Reminder: Appointment for ";
                var maxDescLength = 160 - prefix.Length - suffix.Length;
                var desc = TruncateForSms(schedule.Description ?? schedule.Title, maxDescLength);
                var smsMessage = $"{prefix}{desc}{suffix}";

                await smsService.SendSmsAsync(recipient.PhoneNumber, smsMessage, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder SMS to user {UserId}", recipient.Id);
            }
        }

        // Email (if email exists)
        if (!string.IsNullOrWhiteSpace(recipient.Email) && recipient.Email.Contains('@'))
        {
            try
            {
                if (isCustomer)
                {
                    // For customer: pass provider name if available
                    User? provider = null;
                    if (schedule.ProviderId.HasValue)
                        provider = await session.LoadAsync<User>(schedule.ProviderId.Value, ct);

                    await emailService.SendCustomerReminderAsync(
                        recipient.Email,
                        recipient.FirstName,
                        schedule.Title,
                        schedule.StartTime,
                        schedule.EndTime,
                        provider?.FirstName,
                        booking.ReferenceNumber);
                }
                else
                {
                    await emailService.SendProviderReminderAsync(
                        recipient.Email,
                        recipient.FirstName,
                        schedule.Title,
                        schedule.StartTime,
                        schedule.EndTime,
                        customer?.FirstName ?? "",
                        customer?.LastName ?? "",
                        booking.ReferenceNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder email to user {UserId}", recipient.Id);
            }
        }

        return notified;
    }

    private static string TruncateForSms(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        return string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }
}
