using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Users;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Users;

public sealed class GetUserRequest
{
    public Guid Id { get; set; }
}

public sealed class GetUser : Endpoint<GetUserRequest, UserResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/users/{id}");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Users")
            .WithSummary("Get user by ID")
            .WithDescription("Retrieves a user by their identifier. Users can only view users in their own tenant."));
    }

    public override async Task HandleAsync(GetUserRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        var isGlobalAdmin = User.IsInRole("GlobalAdmin");

        await using var session = DocumentStore.LightweightSession();

        var userId = new UserId(req.Id);
        var user = await session.LoadAsync<User>(userId, ct);

        if (user is null)
        {
            ThrowError("User not found", 404);
        }

        // Enforce tenant isolation via UserTenant membership
        if (!isGlobalAdmin && tenantId.HasValue)
        {
            var membership = await session.Query<UserTenant>()
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId && ut.IsActive, token: ct);
            if (membership is null)
            {
                ThrowError("User not found", 404);
            }
        }
        else if (!isGlobalAdmin)
        {
            ThrowError("User not found", 404);
        }

        Response = new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FirstName = user.FirstName,
            LastName = user.LastName,
            TenantId = user.TenantId,
            IsGlobalAdmin = user.IsGlobalAdmin,
            IsProvider = user.IsProvider,
            CreatedAt = user.CreatedAt,
            IsActive = user.IsActive,
            WorkingHours = user.WorkingHours,
            AutoAcceptBookings = user.AutoAcceptBookings,
            IsPriority = user.IsPriority
        };
    }
}
