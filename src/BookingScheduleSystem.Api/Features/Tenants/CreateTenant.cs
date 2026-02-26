using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Tenants;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Tenants;

public sealed class CreateTenant : Endpoint<CreateTenantRequest, TenantResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/tenants");
        AllowAnonymous();
        Description(d => d
            .WithTags("Tenants")
            .WithSummary("Create a new tenant")
            .WithDescription("Creates a new tenant in the system. Admin only operation."));
    }

    public override async Task HandleAsync(CreateTenantRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        // Check for subdomain uniqueness
        var existingTenant = await session.Query<Tenant>()
            .FirstOrDefaultAsync(t => t.Subdomain == req.Subdomain, token: ct);

        if (existingTenant is not null)
        {
            ThrowError("A tenant with this subdomain already exists", 409);
        }

        var tenant = new Tenant
        {
            Id = TenantId.New(),
            Name = req.Name,
            Subdomain = req.Subdomain.ToLowerInvariant(),
            Description = req.Description,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            OperatingHours = req.OperatingHours,
            BannerUrl = req.BannerUrl,
            Location = req.Location
        };

        session.Store(tenant);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation("Created tenant {TenantId} with subdomain {Subdomain}", tenant.Id, tenant.Subdomain);

        HttpContext.Response.StatusCode = 201;
        Response = new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Subdomain = tenant.Subdomain,
            Description = tenant.Description,
            CreatedAt = tenant.CreatedAt,
            IsActive = tenant.IsActive,
            IsInTrial = tenant.IsInTrial,
            TrialStartDate = tenant.TrialStartDate,
            TrialEndDate = tenant.TrialEndDate,
            OwnerId = tenant.OwnerId,
            OperatingHours = tenant.OperatingHours,
            BannerUrl = tenant.BannerUrl,
            Location = tenant.Location,
            LandingPageTemplate = tenant.LandingPageTemplate
        };
    }
}
