using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Tenants;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Tenants;

public sealed class CreateOrganization : Endpoint<CreateOrganizationRequest, TenantResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/organizations/create");
        Description(d => d
            .WithTags("Organizations")
            .WithSummary("Create a new organization (Provider only)")
            .WithDescription("Allows a provider to create their own organization with a 7-day trial period. Each provider can only create one organization."));
    }

    public override async Task HandleAsync(CreateOrganizationRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        // Get current user ID
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            ThrowError("User not authenticated", 401);
        }

        var userId = UserId.Parse(userIdClaim);

        // Load current user to verify they are a provider
        var user = await session.LoadAsync<User>(userId, ct);
        if (user is null)
        {
            ThrowError("User not found", 404);
        }

        if (!user.IsProvider)
        {
            ThrowError("Only providers can create organizations", 403);
        }

        // Check if provider already owns an organization
        var existingOrganization = await session.Query<Tenant>()
            .FirstOrDefaultAsync(t => t.OwnerId == userId, token: ct);

        if (existingOrganization is not null)
        {
            ThrowError("Provider already owns an organization", 409);
        }

        // Check for subdomain uniqueness
        var existingTenant = await session.Query<Tenant>()
            .FirstOrDefaultAsync(t => t.Subdomain == req.Subdomain, token: ct);

        if (existingTenant is not null)
        {
            ThrowError("A tenant with this subdomain already exists", 409);
        }

        // Create new tenant with 7-day trial
        var trialStartDate = DateTime.UtcNow;
        var trialEndDate = trialStartDate.AddDays(7);

        var tenant = new Tenant
        {
            Id = TenantId.New(),
            Name = req.Name,
            Subdomain = req.Subdomain.ToLowerInvariant(),
            Description = req.Description,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsInTrial = true,
            TrialStartDate = trialStartDate,
            TrialEndDate = trialEndDate,
            OwnerId = userId,
            OperatingHours = req.OperatingHours,
            BannerUrl = req.BannerUrl,
            Location = req.Location
        };

        session.Store(tenant);

        // Update user to associate them with the new tenant
        user.TenantId = tenant.Id;
        session.Update(user);

        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Provider {UserId} created organization {TenantId} with 7-day trial ending at {TrialEndDate}",
            userId, tenant.Id, trialEndDate);

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
