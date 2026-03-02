using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Auth;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Auth;

public sealed class Register : Endpoint<RegisterUserRequest, AuthenticationResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required IPasswordHasher PasswordHasher { get; init; }
    public required IJwtTokenService JwtTokenService { get; init; }

    public override void Configure()
    {
        Post("/api/auth/register");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("Auth"));
        Description(d => d
            .WithTags("Authentication")
            .WithSummary("Register a new user")
            .WithDescription("Creates a new user account. Requires a valid creation code for tenant users."));
    }

    public override async Task HandleAsync(RegisterUserRequest req, CancellationToken ct)
    {
        // Validate password
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
        {
            ThrowError("Password must be at least 8 characters", 400);
            return;
        }

        if (req.Password.Length > 128)
        {
            ThrowError("Password must not exceed 128 characters", 400);
            return;
        }

        await using var session = DocumentStore.LightweightSession();

        // Determine the identifier (email or phone)
        var email = req.Email?.ToLowerInvariant() ?? req.PhoneNumber ?? "";

        // Check if user already exists
        var existingUser = await session.Query<User>()
            .FirstOrDefaultAsync(u => u.Email == email || (req.PhoneNumber != null && u.PhoneNumber == req.PhoneNumber), token: ct);

        if (existingUser is not null)
        {
            ThrowError("A user with this email or phone number already exists", 409);
        }

        TenantId? tenantId = null;
        var isProvider = false;

        // Validate creation code if provided
        if (!string.IsNullOrWhiteSpace(req.CreationCode))
        {
            var creationCode = await session.Query<CreationCode>()
                .FirstOrDefaultAsync(c => c.Code == req.CreationCode, token: ct);

            if (creationCode is null)
            {
                ThrowError("Invalid creation code", 400);
            }

            if (!creationCode.IsActive)
            {
                ThrowError("Creation code is no longer active", 400);
            }

            if (creationCode.CurrentUses >= creationCode.MaxUses)
            {
                ThrowError("Creation code has reached maximum uses", 400);
            }

            if (creationCode.ExpiresAt.HasValue && creationCode.ExpiresAt.Value < DateTime.UtcNow)
            {
                ThrowError("Creation code has expired", 400);
            }

            tenantId = creationCode.TenantId;
            isProvider = creationCode.IsProviderCode;

            // Increment usage count
            creationCode.CurrentUses++;
            session.Update(creationCode);

            Logger.LogInformation(
                "Creation code {CodeId} used for registration. Uses: {CurrentUses}/{MaxUses}, IsProvider: {IsProvider}",
                creationCode.Id, creationCode.CurrentUses, creationCode.MaxUses, isProvider);
        }
        else if (req.TenantId.HasValue)
        {
            // If TenantId provided without creation code, validate tenant exists
            var tenant = await session.LoadAsync<Tenant>(req.TenantId.Value, ct);
            if (tenant is null)
            {
                ThrowError("Specified tenant does not exist", 404);
            }
            tenantId = req.TenantId;
        }
        else
        {
            // No creation code or tenant ID - use the IsProvider flag from request
            // This allows self-registration as a provider for organization creation
            isProvider = req.IsProvider;

            if (isProvider)
            {
                Logger.LogInformation("User registering as provider to create their own organization");
            }
        }

        // Check user/provider limits when registering under a tenant
        TenantSubscription? subscription = null;
        if (tenantId.HasValue)
        {
            var tenant = await session.LoadAsync<Tenant>(tenantId.Value, ct);
            if (tenant is not null)
            {
                var (planLimits, sub) = await PlanLimitHelper.GetCurrentLimitsAsync(session, tenant, ct);
                subscription = sub;

                if (planLimits is not null)
                {
                    var activeUserCount = await session.Query<UserTenant>()
                        .CountAsync(ut => ut.TenantId == tenantId && ut.IsActive, ct);

                    if (activeUserCount >= planLimits.MaxUsers)
                    {
                        ThrowError($"User limit ({planLimits.MaxUsers}) reached for this plan.", 403);
                    }

                    if (isProvider)
                    {
                        var activeProviderCount = await session.Query<UserTenant>()
                            .CountAsync(ut => ut.TenantId == tenantId && ut.Role == "Provider" && ut.IsActive, ct);

                        if (activeProviderCount >= planLimits.MaxProviders)
                        {
                            ThrowError($"Provider limit ({planLimits.MaxProviders}) reached for this plan.", 403);
                        }
                    }
                }
            }
        }

        // Determine provider working hours
        string? providerWorkingHours = null;
        if (isProvider && tenantId.HasValue)
        {
            var tenant = await session.LoadAsync<Tenant>(tenantId.Value, ct);
            if (tenant is not null)
            {
                if (!string.IsNullOrWhiteSpace(req.WorkingHours))
                {
                    // Validate custom working hours
                    if (!OperatingHoursValidator.AreProviderHoursWithinOrganizationHours(
                        tenant.OperatingHours,
                        req.WorkingHours,
                        out var validationError))
                    {
                        ThrowError(validationError, 400);
                    }
                    providerWorkingHours = req.WorkingHours;
                }
                else
                {
                    // Default to organization operating hours
                    providerWorkingHours = tenant.OperatingHours;
                    Logger.LogInformation(
                        "Provider working hours not provided, defaulting to organization operating hours for tenant {TenantId}",
                        tenantId);
                }
            }
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
            IsProvider = isProvider,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            WorkingHours = providerWorkingHours
        };

        session.Store(user);

        // Create UserTenant record if user belongs to a tenant
        if (tenantId.HasValue)
        {
            var userTenant = new UserTenant
            {
                Id = UserTenantId.New(),
                UserId = user.Id,
                TenantId = tenantId.Value,
                Role = isProvider ? "Provider" : "Customer",
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };
            session.Store(userTenant);
        }

        // Update subscription usage stats if not in trial
        if (subscription is not null)
        {
            subscription.CurrentUsage.ActiveUsers++;
            if (isProvider)
            {
                subscription.CurrentUsage.ActiveProviders++;
            }
            subscription.CurrentUsage.LastUpdated = DateTime.SpecifyKind(DateTimeOffset.UtcNow.DateTime, DateTimeKind.Unspecified);
            session.Update(subscription);
        }

        await session.SaveChangesAsync(ct);

        Logger.LogInformation("Registered new user {UserId} with email {Email}", user.Id, user.Email);

        var token = JwtTokenService.GenerateToken(user);

        Response = new AuthenticationResponse
        {
            Token = token,
            ExpiresAt = JwtTokenService.GetTokenExpiration(),
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            TenantId = user.TenantId,
            IsGlobalAdmin = user.IsGlobalAdmin,
            IsProvider = user.IsProvider
        };
    }
}
