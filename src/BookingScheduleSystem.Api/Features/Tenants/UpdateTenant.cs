using System.Security.Claims;
using System.Text.Json;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Tenants;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Tenants;

public sealed class UpdateTenantEndpointRequest
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? OperatingHours { get; set; }
    public string? BannerUrl { get; set; }
    public string? Location { get; set; }
    public string? LandingPageTemplate { get; set; }
}

public sealed class UpdateTenant : Endpoint<UpdateTenantEndpointRequest, TenantResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Put("/api/tenants/{id}");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Tenants")
            .WithSummary("Update tenant")
            .WithDescription("Updates tenant/organization settings. Only tenant owners and GlobalAdmins can update tenants."));
    }

    public override async Task HandleAsync(UpdateTenantEndpointRequest req, CancellationToken ct)
    {
        var isGlobalAdmin = User.IsInRole("GlobalAdmin");
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(currentUserIdClaim))
        {
            ThrowError("User not authenticated", 401);
        }

        var currentUserId = UserId.Parse(currentUserIdClaim);

        await using var session = DocumentStore.LightweightSession();

        var tenantId = new TenantId(req.Id);
        var tenant = await session.LoadAsync<Tenant>(tenantId, ct);

        if (tenant is null)
        {
            ThrowError("Tenant not found", 404);
        }

        // Authorization: tenant owner or GlobalAdmin only
        if (!isGlobalAdmin)
        {
            var contextTenantId = TenantContext.CurrentTenantId;
            if (contextTenantId is null || contextTenantId != tenantId)
            {
                ThrowError("Cannot update a tenant you don't belong to", 403);
            }

            if (tenant.OwnerId != currentUserId)
            {
                ThrowError("Only the tenant owner or a GlobalAdmin can update tenant settings", 403);
            }
        }

        // Validate OperatingHours JSON format if provided
        if (!string.IsNullOrWhiteSpace(req.OperatingHours))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, OperatingHoursValidator.DaySchedule>>(
                    req.OperatingHours,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed is null)
                {
                    ThrowError("Invalid operating hours format", 400);
                }
            }
            catch (JsonException)
            {
                ThrowError("Invalid operating hours JSON format. Expected format: {\"Monday\": {\"IsOpen\": true, \"OpenTime\": \"09:00\", \"CloseTime\": \"17:00\"}, ...}", 400);
            }
        }

        // Apply updates
        tenant.Name = req.Name;
        tenant.Description = req.Description;
        tenant.OperatingHours = req.OperatingHours;
        tenant.BannerUrl = req.BannerUrl;
        tenant.Location = req.Location;
        tenant.LandingPageTemplate = req.LandingPageTemplate;

        session.Update(tenant);
        await session.SaveChangesAsync(ct);

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
