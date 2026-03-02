using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Users;

public sealed class DeactivateUserRequest
{
    public Guid Id { get; set; }
}

public sealed class DeactivateUser : Endpoint<DeactivateUserRequest>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Post("/api/users/{id}/deactivate");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Users")
            .WithSummary("Deactivate user")
            .WithDescription("Deactivates a user account. Only tenant owners and GlobalAdmins can deactivate users. Cannot deactivate self or GlobalAdmins."));
    }

    public override async Task HandleAsync(DeactivateUserRequest req, CancellationToken ct)
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

        // Enforce tenant isolation via UserTenant membership
        if (!isGlobalAdmin && tenantId.HasValue)
        {
            var membership = await session.Query<UserTenant>()
                .FirstOrDefaultAsync(ut => ut.UserId == targetUserId && ut.TenantId == tenantId && ut.IsActive, token: ct);
            if (membership is null)
            {
                ThrowError("User not found", 404);
            }
        }
        else if (!isGlobalAdmin)
        {
            ThrowError("User not found", 404);
        }

        // Cannot deactivate self
        if (currentUserId == targetUserId)
        {
            ThrowError("Cannot deactivate your own account", 400);
        }

        // Cannot deactivate GlobalAdmins
        if (targetUser.IsGlobalAdmin)
        {
            ThrowError("Cannot deactivate a GlobalAdmin", 403);
        }

        // Authorization: tenant owner or GlobalAdmin only
        if (!isGlobalAdmin)
        {
            if (tenantId is null)
            {
                ThrowError("Tenant context is required", 400);
            }

            var tenant = await session.LoadAsync<Tenant>(tenantId.Value, ct);
            if (tenant?.OwnerId != currentUserId)
            {
                ThrowError("Only the tenant owner or a GlobalAdmin can deactivate users", 403);
            }
        }

        targetUser.IsActive = false;

        session.Update(targetUser);
        await session.SaveChangesAsync(ct);
    }
}
