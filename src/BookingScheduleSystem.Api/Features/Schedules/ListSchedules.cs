using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Schedules;

public sealed class ListSchedulesRequest
{
    public Guid? ProviderId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsActive { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ListSchedulesResponse
{
    public required List<ScheduleResponse> Schedules { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}

public sealed class ListSchedules : Endpoint<ListSchedulesRequest, ListSchedulesResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/schedules");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Schedules")
            .WithSummary("List schedules")
            .WithDescription("Retrieves paginated list of schedules for the authenticated tenant with optional filters."));
    }

    public override async Task HandleAsync(ListSchedulesRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
        {
            ThrowError("Tenant context is required", 400);
        }

        await using var session = DocumentStore.LightweightSession();

        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        IQueryable<Schedule> query = session.Query<Schedule>()
            .Where(s => s.TenantId == tenantId);

        if (req.ProviderId.HasValue)
        {
            var providerId = new UserId(req.ProviderId.Value);
            query = query.Where(s => s.ProviderId == providerId);
        }

        if (req.StartDate.HasValue)
        {
            query = query.Where(s => s.StartTime >= req.StartDate.Value);
        }

        if (req.EndDate.HasValue)
        {
            query = query.Where(s => s.EndTime <= req.EndDate.Value);
        }

        if (req.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == req.IsActive.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var schedules = await query
            .OrderBy(s => s.StartTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        Response = new ListSchedulesResponse
        {
            Schedules = schedules.Select(s => new ScheduleResponse
            {
                Id = s.Id,
                TenantId = s.TenantId,
                ProviderId = s.ProviderId,
                Title = s.Title,
                Description = s.Description,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                MaxCapacity = s.MaxCapacity,
                CurrentBookings = s.CurrentBookings,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt
            }).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
