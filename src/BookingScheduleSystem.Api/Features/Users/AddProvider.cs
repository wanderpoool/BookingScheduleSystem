using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Users;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Users;

public sealed class AddProviderRequest
{
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Password { get; set; }
}

public sealed class AddProvider : Endpoint<AddProviderRequest, UserResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }
    public required IPasswordHasher PasswordHasher { get; init; }

    public override void Configure()
    {
        Post("/api/users/add-provider");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Users")
            .WithSummary("Add a new provider")
            .WithDescription("Creates a new provider user under the current tenant. Only tenant owners and GlobalAdmins can add providers."));
    }

    public override async Task HandleAsync(AddProviderRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        var isGlobalAdmin = User.IsInRole("GlobalAdmin");
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(currentUserIdClaim))
        {
            ThrowError("User not authenticated", 401);
        }

        if (tenantId is null)
        {
            ThrowError("Tenant context is required", 400);
        }

        if (string.IsNullOrWhiteSpace(req.Email) && string.IsNullOrWhiteSpace(req.PhoneNumber))
        {
            ThrowError("Email or phone number is required", 400);
        }

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
        {
            ThrowError("Password must be at least 6 characters", 400);
        }

        var currentUserId = UserId.Parse(currentUserIdClaim);

        await using var session = DocumentStore.LightweightSession();

        // Authorization: tenant owner or GlobalAdmin only
        if (!isGlobalAdmin)
        {
            var tenant = await session.LoadAsync<Tenant>(tenantId.Value, ct);
            if (tenant?.OwnerId != currentUserId)
            {
                ThrowError("Only the tenant owner or a GlobalAdmin can add providers", 403);
            }
        }

        // Check if user already exists
        var email = req.Email?.ToLowerInvariant() ?? req.PhoneNumber ?? "";
        var existingUser = await session.Query<User>()
            .FirstOrDefaultAsync(u => u.Email == email || (req.PhoneNumber != null && u.PhoneNumber == req.PhoneNumber), token: ct);

        if (existingUser is not null)
        {
            ThrowError("A user with this email or phone number already exists", 409);
        }

        // Check provider limits
        var tenantEntity = await session.LoadAsync<Tenant>(tenantId.Value, ct);
        TenantSubscription? subscription = null;

        if (tenantEntity is not null)
        {
            var (planLimits, sub) = await PlanLimitHelper.GetCurrentLimitsAsync(session, tenantEntity, ct);
            subscription = sub;

            if (planLimits is not null)
            {
                var activeUserCount = await session.Query<User>()
                    .CountAsync(u => u.TenantId == tenantId && u.IsActive, ct);

                if (activeUserCount >= planLimits.MaxUsers)
                {
                    ThrowError($"User limit ({planLimits.MaxUsers}) reached for this plan.", 403);
                }

                var activeProviderCount = await session.Query<User>()
                    .CountAsync(u => u.TenantId == tenantId && u.IsProvider && u.IsActive, ct);

                if (activeProviderCount >= planLimits.MaxProviders)
                {
                    ThrowError($"Provider limit ({planLimits.MaxProviders}) reached for this plan.", 403);
                }
            }
        }

        // Determine provider working hours (default to org hours)
        string? providerWorkingHours = null;
        if (tenantEntity is not null)
        {
            providerWorkingHours = tenantEntity.OperatingHours;
        }

        var user = new User
        {
            Id = UserId.New(),
            Email = email,
            PhoneNumber = req.PhoneNumber,
            PasswordHash = PasswordHasher.HashPassword(req.Password),
            FirstName = req.FirstName,
            LastName = req.LastName,
            TenantId = tenantId,
            IsGlobalAdmin = false,
            IsProvider = true,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            WorkingHours = providerWorkingHours
        };

        session.Store(user);

        // Update subscription usage stats
        if (subscription is not null)
        {
            subscription.CurrentUsage.ActiveUsers++;
            subscription.CurrentUsage.ActiveProviders++;
            subscription.CurrentUsage.LastUpdated = DateTime.SpecifyKind(DateTimeOffset.UtcNow.DateTime, DateTimeKind.Unspecified);
            session.Update(subscription);
        }

        await session.SaveChangesAsync(ct);

        Logger.LogInformation("Provider {UserId} added to tenant {TenantId} by {CurrentUserId}", user.Id, tenantId, currentUserId);

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
            AutoAcceptBookings = user.AutoAcceptBookings
        };
    }
}
