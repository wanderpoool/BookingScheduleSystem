using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Auth;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Auth;

public sealed class Login : Endpoint<LoginRequest, AuthenticationResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required IPasswordHasher PasswordHasher { get; init; }
    public required IJwtTokenService JwtTokenService { get; init; }
    public required ITenantContext TenantContext { get; init; }
    public required LazyEnrollmentService LazyEnrollment { get; init; }

    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("Auth"));
        Description(d => d
            .WithTags("Authentication")
            .WithSummary("Login with email and password")
            .WithDescription("Authenticates a user and returns a JWT token."));
    }

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        if (string.IsNullOrWhiteSpace(req.Email) && string.IsNullOrWhiteSpace(req.PhoneNumber))
        {
            ThrowError("Email or phone number is required", 400);
            return;
        }

        User? user;
        if (!string.IsNullOrWhiteSpace(req.PhoneNumber))
        {
            user = await session.Query<User>()
                .FirstOrDefaultAsync(u => u.PhoneNumber == req.PhoneNumber, token: ct);
        }
        else
        {
            user = await session.Query<User>()
                .FirstOrDefaultAsync(u => u.Email == req.Email!.ToLowerInvariant(), token: ct);
        }

        // Check account lockout before verifying credentials
        if (user is not null && user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            var remainingMinutes = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            Logger.LogWarning("Login attempt on locked account {Email}, locked for {Minutes} more minutes",
                user.Email, remainingMinutes);
            ThrowError($"Account is temporarily locked. Please try again in {remainingMinutes} minutes.", 423);
            return;
        }

        if (user is null || !PasswordHasher.VerifyPassword(req.Password, user.PasswordHash))
        {
            // Track failed attempts if user exists
            if (user is not null)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                    Logger.LogWarning("Account {Email} locked after {Attempts} failed login attempts",
                        user.Email, user.FailedLoginAttempts);
                }
                session.Update(user);
                await session.SaveChangesAsync(ct);
            }

            ThrowError("Invalid credentials", 401);
            return;
        }

        if (!user.IsActive)
        {
            ThrowError("User account is deactivated", 403);
            return;
        }

        // Reset failed attempts on successful login
        if (user.FailedLoginAttempts > 0)
        {
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            session.Update(user);
            await session.SaveChangesAsync(ct);
        }

        // Determine the active tenant for this session
        var requestedTenantId = TenantContext.CurrentTenantId;
        TenantId? activeTenantId = user.TenantId;

        if (requestedTenantId.HasValue)
        {
            var membership = await LazyEnrollment.EnsureEnrollmentAsync(session, user, requestedTenantId.Value, ct);
            if (membership is null)
            {
                ThrowError("You do not have access to this organization", 403);
                return;
            }
            activeTenantId = requestedTenantId;
            await session.SaveChangesAsync(ct);
        }

        Logger.LogInformation("User {UserId} logged in successfully for tenant {TenantId}", user.Id, activeTenantId);

        var token = activeTenantId.HasValue
            ? JwtTokenService.GenerateToken(user, activeTenantId.Value)
            : JwtTokenService.GenerateToken(user);

        Response = new AuthenticationResponse
        {
            Token = token,
            ExpiresAt = JwtTokenService.GetTokenExpiration(),
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            TenantId = activeTenantId,
            IsGlobalAdmin = user.IsGlobalAdmin,
            IsProvider = user.IsProvider
        };
    }
}
