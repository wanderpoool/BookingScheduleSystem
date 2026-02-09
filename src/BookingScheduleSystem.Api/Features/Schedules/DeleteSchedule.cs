using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Schedules;

public sealed class DeleteScheduleRequest
{
    public Guid ScheduleId { get; set; }
}

public sealed class DeleteSchedule : Endpoint<DeleteScheduleRequest>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Delete("/api/schedules/{ScheduleId}");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Schedules")
            .WithSummary("Delete a schedule")
            .WithDescription("Soft deletes a schedule by marking it as inactive. Prevents deletion if there are active bookings."));
    }

    public override async Task HandleAsync(DeleteScheduleRequest req, CancellationToken ct)
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

        if (schedule.CurrentBookings > 0)
        {
            ThrowError($"Cannot delete schedule with active bookings ({schedule.CurrentBookings} bookings)", 400);
        }

        // Soft delete by marking as inactive
        schedule.IsActive = false;
        session.Update(schedule);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation("Deleted schedule {ScheduleId} for tenant {TenantId}", schedule.Id, schedule.TenantId);
    }
}
