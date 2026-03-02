using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Users;

public sealed class DeleteUserRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteUser : Endpoint<DeleteUserRequest>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Delete("/api/users/{id}");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Users")
            .WithSummary("Delete user")
            .WithDescription("Permanently deletes a user account. Only tenant owners and GlobalAdmins can delete users."));
    }

    public override async Task HandleAsync(DeleteUserRequest req, CancellationToken ct)
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

        // Cannot delete self
        if (currentUserId == targetUserId)
        {
            ThrowError("Cannot delete your own account", 400);
        }

        // Cannot delete GlobalAdmins
        if (targetUser.IsGlobalAdmin)
        {
            ThrowError("Cannot delete a GlobalAdmin", 403);
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
                ThrowError("Only the tenant owner or a GlobalAdmin can delete users", 403);
            }
        }

        session.Delete(targetUser);
        await session.SaveChangesAsync(ct);
    }
}
