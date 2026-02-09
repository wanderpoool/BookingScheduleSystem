using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Responses;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Tenants;

public sealed class GetTenantRequest
{
    public Guid Id { get; set; }
}

public sealed class GetTenant : Endpoint<GetTenantRequest, TenantResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/tenants/{id}");
        AllowAnonymous();
        Description(d => d
            .WithTags("Tenants")
            .WithSummary("Get tenant by ID")
            .WithDescription("Retrieves a tenant by its identifier"));
    }

    public override async Task HandleAsync(GetTenantRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var tenantId = new TenantId(req.Id);
        var tenant = await session.LoadAsync<Tenant>(tenantId, ct);

        if (tenant is null)
        {
            ThrowError("Tenant not found", 404);
        }

        Response = new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Subdomain = tenant.Subdomain,
            Description = tenant.Description,
            CreatedAt = tenant.CreatedAt,
            IsActive = tenant.IsActive
        };
    }
}
