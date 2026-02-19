using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Contracts.Auth;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Auth;

public sealed class LoginWithOtp : Endpoint<LoginWithOtpRequest, AuthenticationResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required IJwtTokenService JwtTokenService { get; init; }
    public required OtpService OtpService { get; init; }

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

        // Validate the OTP verification token
        var isValid = await OtpService.ValidateVerificationTokenAsync(identifier, req.OtpVerificationToken, "login", ct);
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

        Logger.LogInformation("User {UserId} logged in via OTP successfully", user.Id);

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
