using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Auth;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Auth;

public sealed class LoginWithOtp : Endpoint<LoginWithOtpRequest, AuthenticationResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required IJwtTokenService JwtTokenService { get; init; }
    public required OtpService OtpService { get; init; }
    public required ITenantContext TenantContext { get; init; }
    public required LazyEnrollmentService LazyEnrollment { get; init; }

    public override void Configure()
    {
        Post("/api/auth/login-otp");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("Auth"));
        Description(d => d
            .WithTags("Authentication")
            .WithSummary("Login with OTP verification")
            .WithDescription("Authenticates a user using a verified OTP token and returns a JWT token."));
    }

    public override async Task HandleAsync(LoginWithOtpRequest req, CancellationToken ct)
    {
        // Determine the identifier based on contact method
        var identifier = req.ContactMethod.ToLowerInvariant() switch
        {
            "email" => req.Email?.ToLowerInvariant(),
            "phone" => req.PhoneNumber,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(identifier))
        {
            ThrowError("Email or phone number is required based on contact method", 400);
            return;
        }

        // Validate the OTP verification token — try "login" purpose first,
        // then fall back to "registration" (chatbot flow uses registration purpose for auto-login)
        var isValid = await OtpService.ValidateVerificationTokenAsync(identifier, req.OtpVerificationToken, "login", ct)
                   || await OtpService.ValidateVerificationTokenAsync(identifier, req.OtpVerificationToken, "registration", ct);
        if (!isValid)
        {
            ThrowError("Invalid or expired OTP verification token", 401);
            return;
        }

        await using var session = DocumentStore.LightweightSession();

        // Look up user by email or phone
        User? user = req.ContactMethod.ToLowerInvariant() switch
        {
            "email" => await session.Query<User>()
                .FirstOrDefaultAsync(u => u.Email == identifier, token: ct),
            "phone" => await session.Query<User>()
                .FirstOrDefaultAsync(u => u.PhoneNumber == identifier, token: ct),
            _ => null
        };

        if (user is null)
        {
            ThrowError("No account found with this contact information", 404);
            return;
        }

        if (!user.IsActive)
        {
            ThrowError("User account is deactivated", 403);
            return;
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

        Logger.LogInformation("User {UserId} logged in via OTP successfully for tenant {TenantId}", user.Id, activeTenantId);

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
