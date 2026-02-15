using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Users;

public sealed class AdminResetPasswordRequest
{
    public Guid Id { get; set; }
    public required string NewPassword { get; set; }
}

public sealed class AdminResetPassword : Endpoint<AdminResetPasswordRequest>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }
    public required IPasswordHasher PasswordHasher { get; init; }

    public override void Configure()
    {
        Post("/api/users/{id}/reset-password");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Users")
            .WithSummary("Reset user password (Admin)")
            .WithDescription("Resets a user's password. Only tenant owners and GlobalAdmins can reset passwords."));
    }

    public override async Task HandleAsync(AdminResetPasswordRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        var isGlobalAdmin = User.IsInRole("GlobalAdmin");
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(currentUserIdClaim))
        {
            ThrowError("User not authenticated", 401);
        }

        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
        {
            ThrowError("Password must be at least 6 characters", 400);
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

        // Cannot reset GlobalAdmin passwords unless you are GlobalAdmin
        if (targetUser.IsGlobalAdmin && !isGlobalAdmin)
        {
            ThrowError("Cannot reset a GlobalAdmin's password", 403);
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
                ThrowError("Only the tenant owner or a GlobalAdmin can reset passwords", 403);
            }
        }

        targetUser.PasswordHash = PasswordHasher.HashPassword(req.NewPassword);

        session.Update(targetUser);
        await session.SaveChangesAsync(ct);
    }
}
