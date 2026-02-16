using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Users;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Users;

public sealed class UpdateUserEndpointRequest
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? WorkingHours { get; set; }
    public Guid? TenantId { get; set; }
}

public sealed class UpdateUser : Endpoint<UpdateUserEndpointRequest, UserResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Put("/api/users/{id}");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Users")
            .WithSummary("Update user")
            .WithDescription("Updates a user's profile. Users can edit themselves; tenant owners and GlobalAdmins can edit any user in their tenant."));
    }

    public override async Task HandleAsync(UpdateUserEndpointRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        var isGlobalAdmin = User.IsInRole("GlobalAdmin");
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(currentUserIdClaim))
        {
            ThrowError("User not authenticated", 401);
        }

        var currentUserId = UserId.Parse(currentUserIdClaim);

        await using var session = DocumentStore.LightweightSession();

        var targetUserId = new UserId(req.Id);
        var targetUser = await session.LoadAsync<User>(targetUserId, ct);

        if (targetUser is null)
        {
            ThrowError("User not found", 404);
        }

        // Enforce tenant isolation
        if (!isGlobalAdmin && targetUser.TenantId != tenantId)
        {
            ThrowError("User not found", 404);
        }

        // Authorization: self-edit, tenant owner, or GlobalAdmin
        var isSelfEdit = currentUserId == targetUserId;
        var isTenantOwner = false;

        if (tenantId is not null && !isSelfEdit && !isGlobalAdmin)
        {
            var tenant = await session.LoadAsync<Tenant>(tenantId.Value, ct);
            isTenantOwner = tenant?.OwnerId == currentUserId;

            if (!isTenantOwner)
            {
                ThrowError("Only the user themselves, tenant owner, or GlobalAdmin can update a user", 403);
            }
        }

        // Validate WorkingHours against organization OperatingHours
        if (!string.IsNullOrWhiteSpace(req.WorkingHours) && targetUser.TenantId is not null)
        {
            var tenant = await session.LoadAsync<Tenant>(targetUser.TenantId.Value, ct);
            if (tenant is not null)
            {
                if (!OperatingHoursValidator.AreProviderHoursWithinOrganizationHours(
                    tenant.OperatingHours, req.WorkingHours, out var errorMessage))
                {
                    ThrowError(errorMessage, 400);
                }
            }
        }

        // Apply updates (only non-null fields)
        if (req.FirstName is not null)
            targetUser.FirstName = req.FirstName;

        if (req.LastName is not null)
            targetUser.LastName = req.LastName;

        if (req.PhoneNumber is not null)
            targetUser.PhoneNumber = req.PhoneNumber;

        if (req.WorkingHours is not null)
            targetUser.WorkingHours = req.WorkingHours;

        // TenantId can only be changed by GlobalAdmin
        if (req.TenantId.HasValue)
        {
            if (!isGlobalAdmin)
            {
                ThrowError("Only a GlobalAdmin can change a user's tenant", 403);
            }

            var newTenantId = new TenantId(req.TenantId.Value);
            var newTenant = await session.LoadAsync<Tenant>(newTenantId, ct);
            if (newTenant is null)
            {
                ThrowError("Target tenant does not exist", 404);
            }

            targetUser.TenantId = newTenantId;
        }

        session.Update(targetUser);
        await session.SaveChangesAsync(ct);

        Response = new UserResponse
        {
            Id = targetUser.Id,
            Email = targetUser.Email,
            PhoneNumber = targetUser.PhoneNumber,
            FirstName = targetUser.FirstName,
            LastName = targetUser.LastName,
            TenantId = targetUser.TenantId,
            IsGlobalAdmin = targetUser.IsGlobalAdmin,
            IsProvider = targetUser.IsProvider,
            CreatedAt = targetUser.CreatedAt,
            IsActive = targetUser.IsActive,
            WorkingHours = targetUser.WorkingHours
        };
    }
}
