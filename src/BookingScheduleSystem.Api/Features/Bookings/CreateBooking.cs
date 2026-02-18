using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Bookings;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Bookings;

public sealed class CreateBooking : Endpoint<CreateBookingRequest, BookingResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }
    public required BookingNotificationService NotificationService { get; init; }
    public required BookingEmailNotificationService EmailNotificationService { get; init; }

    public override void Configure()
    {
        Post("/api/bookings");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Bookings")
            .WithSummary("Create a new booking")
            .WithDescription("Creates a new booking for an available schedule slot."));
    }

    public override async Task HandleAsync(CreateBookingRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
        {
            ThrowError("Tenant context is required", 400);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            ThrowError("User not authenticated", 401);
        }

        var userId = UserId.Parse(userIdClaim);

        await using var session = DocumentStore.LightweightSession();

        // Load tenant and check subscription/trial limits
        var tenant = await session.LoadAsync<Tenant>(tenantId.Value, ct);
        if (tenant is null)
        {
            ThrowError("Tenant not found", 404);
        }

        // Get plan limits (either from trial or subscription)
        var (planLimits, subscription) = await PlanLimitHelper.GetCurrentLimitsAsync(session, tenant, ct);

        if (planLimits is not null)
        {
            // Check daily booking limit
            // Marten uses 'timestamp without time zone' — must use DateTime with Kind=Unspecified
            var today = DateTime.SpecifyKind(DateTimeOffset.UtcNow.Date, DateTimeKind.Unspecified);
            var tomorrow = today.AddDays(1);

            var todayBookingCount = await session.Query<Booking>()
                .CountAsync(b => b.TenantId == tenantId.Value
                    && b.BookedAt >= today
                    && b.BookedAt < tomorrow
                    && b.Status != BookingStatus.Cancelled, ct);

            if (todayBookingCount >= planLimits.MaxBookingsPerDay)
            {
                ThrowError($"Daily booking limit ({planLimits.MaxBookingsPerDay}) reached for today.", 403);
            }

            // Check monthly booking limit
            var now = DateTimeOffset.UtcNow;
            var firstDayOfMonth = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1), DateTimeKind.Unspecified);
            var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);

            var monthlyBookingCount = await session.Query<Booking>()
                .CountAsync(b => b.TenantId == tenantId.Value
                    && b.BookedAt >= firstDayOfMonth
                    && b.BookedAt < firstDayOfNextMonth
                    && b.Status != BookingStatus.Cancelled, ct);

            if (monthlyBookingCount >= planLimits.MaxBookingsPerMonth)
            {
                ThrowError($"Monthly booking limit ({planLimits.MaxBookingsPerMonth}) reached for this month.", 403);
            }

            // Check concurrent booking limit (active bookings for future schedules)
            var nowUtc = DateTime.SpecifyKind(DateTimeOffset.UtcNow.DateTime, DateTimeKind.Unspecified);

            var futureScheduleIds = await session.Query<Schedule>()
                .Where(s => s.TenantId == tenantId.Value && s.StartTime > nowUtc)
                .Select(s => s.Id)
                .ToListAsync(ct);

            // Marten can't serialize List<ScheduleId> for SQL array params — filter in-memory
            var activeBookings = await session.Query<Booking>()
                .Where(b => b.TenantId == tenantId.Value
                    && b.Status != BookingStatus.Cancelled)
                .ToListAsync(ct);

            var futureScheduleIdSet = futureScheduleIds.ToHashSet();
            var concurrentBookingCount = activeBookings.Count(b => futureScheduleIdSet.Contains(b.ScheduleId));

            if (concurrentBookingCount >= planLimits.MaxConcurrentBookings)
            {
                ThrowError($"Concurrent booking limit ({planLimits.MaxConcurrentBookings}) reached.", 403);
            }
        }

        var scheduleId = new ScheduleId(req.ScheduleId);
        var schedule = await session.LoadAsync<Schedule>(scheduleId, ct);

        if (schedule is null)
        {
            ThrowError("Schedule not found", 404);
        }

        if (schedule.TenantId != tenantId)
        {
            ThrowError("Schedule not found", 404);
        }

        if (!schedule.IsActive)
        {
            ThrowError("Schedule is not active", 400);
        }

        if (schedule.StartTime <= DateTime.SpecifyKind(DateTimeOffset.UtcNow.DateTime, DateTimeKind.Unspecified))
        {
            ThrowError("Cannot book a schedule that has already started or passed", 400);
        }

        if (schedule.CurrentBookings >= schedule.MaxCapacity)
        {
            ThrowError("Schedule is fully booked", 409);
        }

        // Check if user already has a booking for this schedule
        var existingBooking = await session.Query<Booking>()
            .FirstOrDefaultAsync(b => b.ScheduleId == scheduleId
                && b.UserId == userId
                && b.Status != BookingStatus.Cancelled, ct);

        if (existingBooking is not null)
        {
            ThrowError("You already have a booking for this schedule", 409);
        }

        // Determine booking status based on provider auto-accept setting
        User? provider = null;
        var bookingStatus = BookingStatus.Confirmed;

        if (schedule.ProviderId.HasValue)
        {
            provider = await session.LoadAsync<User>(schedule.ProviderId.Value, ct);
            bookingStatus = provider is { AutoAcceptBookings: true }
                ? BookingStatus.Confirmed
                : BookingStatus.Pending;
        }

        var booking = new Booking
        {
            Id = BookingId.New(),
            ScheduleId = scheduleId,
            UserId = userId,
            TenantId = tenantId.Value,
            Status = bookingStatus,
            Notes = req.Notes,
            BookedAt = DateTime.SpecifyKind(DateTimeOffset.UtcNow.DateTime, DateTimeKind.Unspecified)
        };

        schedule.CurrentBookings++;

        session.Store(booking);
        session.Update(schedule);

        // Update subscription usage stats if not in trial
        if (subscription is not null)
        {
            subscription.CurrentUsage.BookingsToday++;
            subscription.CurrentUsage.BookingsThisMonth++;
            subscription.CurrentUsage.LastUpdated = DateTime.SpecifyKind(DateTimeOffset.UtcNow.DateTime, DateTimeKind.Unspecified);
            session.Update(subscription);
        }

        // Create in-app notifications
        NotificationService.NotifyBookingCreated(session, booking, schedule);

        await session.SaveChangesAsync(ct);

        // Send email to provider (fire-and-forget — failure must not block response)
        if (provider is not null)
        {
            var customer = await session.LoadAsync<User>(userId, ct);
            if (customer is not null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (bookingStatus == BookingStatus.Confirmed)
                            await EmailNotificationService.SendBookingConfirmedEmailAsync(provider, booking, schedule, customer);
                        else
                            await EmailNotificationService.SendBookingApprovalEmailAsync(provider, booking, schedule, customer);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to send booking email for {BookingId}", booking.Id);
                    }
                });
            }
        }

        Logger.LogInformation(
            "Created booking {BookingId} for schedule {ScheduleId} by user {UserId} with status {Status}",
            booking.Id, booking.ScheduleId, booking.UserId, booking.Status);

        Response = new BookingResponse
        {
            Id = booking.Id,
            ScheduleId = booking.ScheduleId,
            UserId = booking.UserId,
            TenantId = booking.TenantId,
            Status = booking.Status,
            Notes = booking.Notes,
            BookedAt = booking.BookedAt,
            CancelledAt = booking.CancelledAt,
            CancellationReason = booking.CancellationReason
        };
    }
}
