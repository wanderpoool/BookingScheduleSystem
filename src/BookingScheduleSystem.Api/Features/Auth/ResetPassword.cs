using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Contracts.Auth;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Auth;

public sealed class ResetPassword : Endpoint<ResetPasswordRequest>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required IPasswordHasher PasswordHasher { get; init; }
    public required OtpService OtpService { get; init; }

    public override void Configure()
    {
        Post("/api/auth/reset-password");
        AllowAnonymous();
        Description(d => d
            .WithTags("Authentication")
            .WithSummary("Reset password using OTP verification token")
            .WithDescription("Resets a user's password after verifying OTP token from the password-reset flow."));
    }

    public override async Task HandleAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
        {
            ThrowError("Password must be at least 6 characters");
            return;
        }

        if (string.IsNullOrWhiteSpace(req.OtpVerificationToken))
        {
            ThrowError("Verification token is required");
            return;
        }

        // Resolve identifier the same way VerifyOtp does
        string identifier;
        if (req.ContactMethod.ToLower() == "email")
        {
            if (string.IsNullOrWhiteSpace(req.Email))
            {
                ThrowError("Email is required when using email contact method");
                return;
            }
            identifier = req.Email.ToLower();
        }
        else if (req.ContactMethod.ToLower() == "phone")
        {
            if (string.IsNullOrWhiteSpace(req.PhoneNumber))
            {
                ThrowError("Phone number is required when using phone contact method");
                return;
            }
            identifier = req.PhoneNumber;
        }
        else
        {
            ThrowError("Contact method must be 'email' or 'phone'");
            return;
        }

        // Validate OTP verification token
        var isTokenValid = OtpService.ValidateVerificationToken(identifier, req.OtpVerificationToken, "password-reset");
        if (!isTokenValid)
        {
            ThrowError("Invalid or expired verification token. Please request a new password reset.");
            return;
        }

        await using var session = DocumentStore.LightweightSession();

        User? user;
        if (req.ContactMethod.ToLower() == "email")
        {
            user = await session.Query<User>()
                .FirstOrDefaultAsync(u => u.Email == identifier, token: ct);
        }
        else
        {
            user = await session.Query<User>()
                .FirstOrDefaultAsync(u => u.PhoneNumber == identifier, token: ct);
        }

        if (user is null)
        {
            ThrowError("No account found with this contact information");
            return;
        }

        if (!user.IsActive)
        {
            ThrowError("This account has been deactivated");
            return;
        }

        user.PasswordHash = PasswordHasher.HashPassword(req.NewPassword);
        session.Update(user);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation("Password reset successfully for user {Identifier}", identifier);

        HttpContext.Response.StatusCode = 200;
    }
}
