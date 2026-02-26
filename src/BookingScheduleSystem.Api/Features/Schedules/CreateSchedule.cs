using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Schedules;

public sealed class CreateSchedule : Endpoint<CreateScheduleRequest, ScheduleResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Post("/api/schedules");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Schedules")
            .WithSummary("Create a new schedule")
            .WithDescription("Creates a new availability schedule for the authenticated tenant."));
    }

    public override async Task HandleAsync(CreateScheduleRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
        {
            ThrowError("Tenant context is required", 400);
        }

        if (req.EndTime <= req.StartTime)
        {
            ThrowError("End time must be after start time", 400);
        }

        if (req.MaxCapacity < 1)
        {
            ThrowError("Max capacity must be at least 1", 400);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            ThrowError("User not authenticated", 401);
        }

        await using var session = DocumentStore.LightweightSession();

        // Load tenant to validate operating hours
        var tenant = await session.LoadAsync<Tenant>(tenantId.Value, ct);
        if (tenant is null)
        {
            ThrowError("Tenant not found", 404);
        }

        // Validate against operating hours
        if (!OperatingHoursValidator.IsWithinOperatingHours(
            tenant.OperatingHours,
            req.StartTime,
            req.EndTime,
            out var operatingHoursError))
        {
            ThrowError(operatingHoursError, 400);
        }

        // Check schedule limits (trial or subscription)
        var (planLimits, subscription) = await PlanLimitHelper.GetCurrentLimitsAsync(session, tenant, ct);

        if (planLimits is not null)
        {
            var today = DateTime.SpecifyKind(DateTimeOffset.UtcNow.Date, DateTimeKind.Unspecified);
            var tomorrow = today.AddDays(1);

            var todayScheduleCount = await session.Query<Schedule>()
                .CountAsync(s => s.TenantId == tenantId.Value
                    && s.CreatedAt >= today
                    && s.CreatedAt < tomorrow
                    && s.IsActive, ct);

            if (todayScheduleCount >= planLimits.MaxSchedulesPerDay)
            {
                ThrowError($"Daily schedule limit ({planLimits.MaxSchedulesPerDay}) reached for today.", 403);
            }

            var now = DateTimeOffset.UtcNow;
            var firstDayOfMonth = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1), DateTimeKind.Unspecified);
            var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);

            var monthlyScheduleCount = await session.Query<Schedule>()
                .CountAsync(s => s.TenantId == tenantId.Value
                    && s.CreatedAt >= firstDayOfMonth
                    && s.CreatedAt < firstDayOfNextMonth
                    && s.IsActive, ct);

            if (monthlyScheduleCount >= planLimits.MaxSchedulesPerMonth)
            {
                ThrowError($"Monthly schedule limit ({planLimits.MaxSchedulesPerMonth}) reached for this month.", 403);
            }
        }

        UserId? providerId = null;
        if (req.ProviderId.HasValue)
        {
            providerId = new UserId(req.ProviderId.Value);

            // Verify provider exists and belongs to the same tenant
            var provider = await session.LoadAsync<Infrastructure.Auth.User>(providerId.Value, ct);
            if (provider is null)
            {
                ThrowError("Provider not found", 404);
            }

            if (provider.TenantId != tenantId)
            {
                ThrowError("Provider must belong to the same tenant", 403);
            }

            // Validate schedule is within provider's working hours
            if (!OperatingHoursValidator.IsWithinOperatingHours(
                provider.WorkingHours,
                req.StartTime,
                req.EndTime,
                out var providerHoursError))
            {
                ThrowError($"Provider availability: {providerHoursError}", 400);
            }

            // Check for overlapping schedules for the same provider
            var hasOverlap = await session.Query<Schedule>()
                .Where(s => s.ProviderId == providerId
                    && s.IsActive
                    && s.TenantId == tenantId
                    && s.StartTime < req.EndTime
                    && s.EndTime > req.StartTime)
                .AnyAsync(ct);

            if (hasOverlap)
            {
                ThrowError("Provider already has a schedule that overlaps with this time range", 409);
            }
        }

        var schedule = new Schedule
        {
            Id = ScheduleId.New(),
            TenantId = tenantId.Value,
            ProviderId = providerId,
            Title = req.Title,
            Description = req.Description,
            StartTime = DateTime.SpecifyKind(req.StartTime, DateTimeKind.Unspecified),
            EndTime = DateTime.SpecifyKind(req.EndTime, DateTimeKind.Unspecified),
            MaxCapacity = req.MaxCapacity,
            CurrentBookings = 0,
            IsActive = true,
            CreatedAt = DateTime.SpecifyKind(DateTimeOffset.UtcNow.DateTime, DateTimeKind.Unspecified),
            CreatedBy = UserId.Parse(userIdClaim)
        };

        session.Store(schedule);

        // Update subscription usage stats if not in trial
        if (subscription is not null)
        {
            subscription.CurrentUsage.SchedulesToday++;
            subscription.CurrentUsage.SchedulesThisMonth++;
            subscription.CurrentUsage.LastUpdated = DateTime.SpecifyKind(DateTimeOffset.UtcNow.DateTime, DateTimeKind.Unspecified);
            session.Update(subscription);
        }

        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Created schedule {ScheduleId} for tenant {TenantId} by user {UserId}",
            schedule.Id, schedule.TenantId, schedule.CreatedBy);

        Response = new ScheduleResponse
        {
            Id = schedule.Id,
            TenantId = schedule.TenantId,
            ProviderId = schedule.ProviderId,
            Title = schedule.Title,
            Description = schedule.Description,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            MaxCapacity = schedule.MaxCapacity,
            CurrentBookings = schedule.CurrentBookings,
            IsActive = schedule.IsActive,
            CreatedAt = schedule.CreatedAt
        };
    }
}
