using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Schedules;

public sealed class UpdateScheduleRequestWithId
{
    public Guid ScheduleId { get; set; }
    public required UpdateScheduleRequest Update { get; set; }
}

public sealed class UpdateSchedule : Endpoint<UpdateScheduleRequestWithId, ScheduleResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Put("/api/schedules/{ScheduleId}");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Schedules")
            .WithSummary("Update a schedule")
            .WithDescription("Updates an existing schedule within the authenticated tenant."));
    }

    public override async Task HandleAsync(UpdateScheduleRequestWithId req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
        {
            ThrowError("Tenant context is required", 400);
        }

        await using var session = DocumentStore.LightweightSession();

        var scheduleId = new ScheduleId(req.ScheduleId);
        var schedule = await session.LoadAsync<Schedule>(scheduleId, ct);

        if (schedule is null)
        {
            ThrowError("Schedule not found", 404);
        }

        // Verify schedule belongs to the authenticated tenant
        if (schedule.TenantId != tenantId)
        {
            ThrowError("Schedule not found", 404);
        }

        var update = req.Update;

        if (update.Title is not null)
        {
            schedule.Title = update.Title;
        }

        if (update.Description is not null)
        {
            schedule.Description = update.Description;
        }

        if (update.StartTime.HasValue)
        {
            schedule.StartTime = update.StartTime.Value;
        }

        if (update.EndTime.HasValue)
        {
            schedule.EndTime = update.EndTime.Value;
        }

        if (schedule.EndTime <= schedule.StartTime)
        {
            ThrowError("End time must be after start time", 400);
        }

        // Validate against operating hours if times were updated
        if (update.StartTime.HasValue || update.EndTime.HasValue)
        {
            var tenant = await session.LoadAsync<Tenant>(tenantId.Value, ct);
            if (tenant is null)
            {
                ThrowError("Tenant not found", 404);
            }

            if (!OperatingHoursValidator.IsWithinOperatingHours(
                tenant.OperatingHours,
                schedule.StartTime,
                schedule.EndTime,
                out var operatingHoursError))
            {
                ThrowError(operatingHoursError, 400);
            }
        }

        if (update.MaxCapacity.HasValue)
        {
            if (update.MaxCapacity.Value < 1)
            {
                ThrowError("Max capacity must be at least 1", 400);
            }

            if (update.MaxCapacity.Value < schedule.CurrentBookings)
            {
                ThrowError($"Cannot reduce max capacity below current bookings ({schedule.CurrentBookings})", 400);
            }

            schedule.MaxCapacity = update.MaxCapacity.Value;
        }

        if (update.IsActive.HasValue)
        {
            schedule.IsActive = update.IsActive.Value;
        }

        // Validate against provider working hours if provider is assigned and times were updated
        if (schedule.ProviderId.HasValue && (update.StartTime.HasValue || update.EndTime.HasValue))
        {
            var provider = await session.LoadAsync<Infrastructure.Auth.User>(schedule.ProviderId.Value, ct);
            if (provider is not null)
            {
                if (!OperatingHoursValidator.IsWithinOperatingHours(
                    provider.WorkingHours,
                    schedule.StartTime,
                    schedule.EndTime,
                    out var providerHoursError))
                {
                    ThrowError($"Provider availability: {providerHoursError}", 400);
                }
            }
        }

        // Check for overlapping schedules if provider is assigned and times were updated
        if (schedule.ProviderId.HasValue && (update.StartTime.HasValue || update.EndTime.HasValue))
        {
            var hasOverlap = await session.Query<Schedule>()
                .Where(s => s.Id != schedule.Id
                    && s.ProviderId == schedule.ProviderId
                    && s.IsActive
                    && s.TenantId == tenantId
                    && s.StartTime < schedule.EndTime
                    && s.EndTime > schedule.StartTime)
                .AnyAsync(ct);

            if (hasOverlap)
            {
                ThrowError("Provider already has a schedule that overlaps with this time range", 409);
            }
        }

        session.Update(schedule);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation("Updated schedule {ScheduleId} for tenant {TenantId}", schedule.Id, schedule.TenantId);

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
