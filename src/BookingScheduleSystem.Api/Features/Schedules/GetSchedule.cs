using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Schedules;

public sealed class GetScheduleRequest
{
    public Guid ScheduleId { get; set; }
}

public sealed class GetSchedule : Endpoint<GetScheduleRequest, ScheduleResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/schedules/{ScheduleId}");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Schedules")
            .WithSummary("Get schedule by ID")
            .WithDescription("Retrieves a specific schedule by ID within the authenticated tenant."));
    }

    public override async Task HandleAsync(GetScheduleRequest req, CancellationToken ct)
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
