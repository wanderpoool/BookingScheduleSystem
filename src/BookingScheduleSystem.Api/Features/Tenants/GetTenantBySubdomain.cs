using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Tenants;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Tenants;

public sealed class GetTenantBySubdomainRequest
{
    public string Subdomain { get; set; } = string.Empty;
}

public sealed class GetTenantBySubdomain : Endpoint<GetTenantBySubdomainRequest, TenantResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/tenants/by-subdomain/{subdomain}");
        AllowAnonymous();
        Description(d => d
            .WithTags("Tenants")
            .WithSummary("Get tenant by subdomain")
            .WithDescription("Retrieves a tenant by its subdomain slug"));
    }

    public override async Task HandleAsync(GetTenantBySubdomainRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var tenant = await session.Query<Tenant>()
            .FirstOrDefaultAsync(t => t.Subdomain == req.Subdomain, token: ct);

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
            IsActive = tenant.IsActive,
            IsInTrial = tenant.IsInTrial,
            TrialStartDate = tenant.TrialStartDate,
            TrialEndDate = tenant.TrialEndDate,
            OwnerId = tenant.OwnerId,
            OperatingHours = tenant.OperatingHours,
            BannerUrl = tenant.BannerUrl,
            Location = tenant.Location,
            LandingPageTemplate = tenant.LandingPageTemplate,
            QueueEnabled = tenant.QueueEnabled,
            QueueAverageServiceTimeMinutes = tenant.QueueAverageServiceTimeMinutes,
            QueueNotificationLeadMinutes = tenant.QueueNotificationLeadMinutes
        };
    }
}
