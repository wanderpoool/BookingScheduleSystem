using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
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
        PlanLimits? planLimits = null;
        TenantSubscription? subscription = null;

        if (tenant.IsInTrial)
        {
            // Trial has fixed limits
            planLimits = new PlanLimits
            {
                MaxBookingsPerDay = 2,
                MaxBookingsPerMonth = 60,
                MaxConcurrentBookings = 5,
                MaxSchedulesPerDay = 5,
                MaxSchedulesPerMonth = 150,
                MaxUsers = 5,
                MaxProviders = 1,
                MaxBranches = 1,
                AllowMultipleBranches = false,
                AllowCustomBranding = false,
                AllowApiAccess = false,
                AllowAdvancedReporting = false,
                AllowPrioritySupport = false
            };
        }
        else
        {
            // Load subscription and plan limits
            subscription = await session.Query<TenantSubscription>()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value && s.Status == SubscriptionStatus.Active, token: ct);

            if (subscription is not null)
            {
                var plan = await session.LoadAsync<SubscriptionPlan>(subscription.PlanId, ct);
                if (plan is not null)
                {
                    planLimits = plan.Limits;
                }
            }
        }

        if (planLimits is not null)
        {
            // Check daily booking limit
            var today = DateTime.UtcNow.Date;
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
            var firstDayOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
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

        if (schedule.StartTime <= DateTime.UtcNow)
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

        // If schedule has a provider, booking requires approval
        var requiresApproval = schedule.ProviderId.HasValue;

        var booking = new Booking
        {
            Id = BookingId.New(),
            ScheduleId = scheduleId,
            UserId = userId,
            TenantId = tenantId.Value,
            Status = requiresApproval ? BookingStatus.Pending : BookingStatus.Confirmed,
            Notes = req.Notes,
            BookedAt = DateTime.UtcNow
        };

        schedule.CurrentBookings++;

        session.Store(booking);
        session.Update(schedule);

        // Update subscription usage stats if not in trial
        if (subscription is not null)
        {
            subscription.CurrentUsage.BookingsToday++;
            subscription.CurrentUsage.BookingsThisMonth++;
            subscription.CurrentUsage.LastUpdated = DateTime.UtcNow;
            session.Update(subscription);
        }

        await session.SaveChangesAsync(ct);

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
