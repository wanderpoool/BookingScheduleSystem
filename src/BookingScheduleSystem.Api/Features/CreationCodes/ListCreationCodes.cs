using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.CreationCodes;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.CreationCodes;

public sealed class ListCreationCodesRequest
{
    public Guid? TenantId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ListCreationCodesResponse
{
    public required List<CreationCodeResponse> CreationCodes { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}

public sealed class ListCreationCodes : Endpoint<ListCreationCodesRequest, ListCreationCodesResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/creation-codes");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("CreationCodes")
            .WithSummary("List creation codes")
            .WithDescription("Retrieves paginated list of creation codes, optionally filtered by tenant. Admin only."));
    }

    public override async Task HandleAsync(ListCreationCodesRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        IQueryable<CreationCode> query = session.Query<CreationCode>();

        if (req.TenantId.HasValue)
        {
            var tenantId = new TenantId(req.TenantId.Value);
            query = query.Where(c => c.TenantId == tenantId);
        }

        var totalCount = await query.CountAsync(ct);

        var codes = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        Response = new ListCreationCodesResponse
        {
            CreationCodes = codes.Select(c => new CreationCodeResponse
            {
                Id = c.Id,
                TenantId = c.TenantId,
                Code = c.Code,
                MaxUses = c.MaxUses,
                CurrentUses = c.CurrentUses,
                ExpiresAt = c.ExpiresAt,
                CreatedAt = c.CreatedAt,
                IsActive = c.IsActive,
                IsProviderCode = c.IsProviderCode
            }).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
