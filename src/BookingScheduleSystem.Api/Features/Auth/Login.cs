using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Contracts.Auth;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Auth;

public sealed class Login : Endpoint<LoginRequest, AuthenticationResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required IPasswordHasher PasswordHasher { get; init; }
    public required IJwtTokenService JwtTokenService { get; init; }

    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
        Description(d => d
            .WithTags("Authentication")
            .WithSummary("Login with email and password")
            .WithDescription("Authenticates a user and returns a JWT token."));
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var user = await session.Query<User>()
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant(), token: ct);

        if (user is null || !PasswordHasher.VerifyPassword(req.Password, user.PasswordHash))
        {
            ThrowError("Invalid email or password", 401);
            return;
        }

        if (!user.IsActive)
        {
            ThrowError("User account is deactivated", 403);
            return;
        }

        Logger.LogInformation("User {UserId} logged in successfully", user.Id);

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
            IsGlobalAdmin = user.IsGlobalAdmin
        };
    }
}
