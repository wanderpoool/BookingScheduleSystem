using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Tenants;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Tenants;

public sealed class ListTenantsRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ListTenantsResponse
{
    public required List<TenantResponse> Tenants { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}

public sealed class ListTenants : Endpoint<ListTenantsRequest, ListTenantsResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/tenants");
        AllowAnonymous();
        Description(d => d
            .WithTags("Tenants")
            .WithSummary("List all tenants")
            .WithDescription("Retrieves a paginated list of all tenants. Admin only operation."));
    }

    public override async Task HandleAsync(ListTenantsRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        var totalCount = await session.Query<Tenant>().CountAsync(ct);

        var tenants = await session.Query<Tenant>()
            .OrderBy(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        Response = new ListTenantsResponse
        {
            Tenants = tenants.Select(t => new TenantResponse
            {
                Id = t.Id,
                Name = t.Name,
                Subdomain = t.Subdomain,
                Description = t.Description,
                CreatedAt = t.CreatedAt,
                IsActive = t.IsActive,
                IsInTrial = t.IsInTrial,
                TrialStartDate = t.TrialStartDate,
                TrialEndDate = t.TrialEndDate,
                OwnerId = t.OwnerId,
                OperatingHours = t.OperatingHours,
                BannerUrl = t.BannerUrl,
                Location = t.Location
            }).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
