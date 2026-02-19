using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Contracts.Auth;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Auth;

public sealed class SendOtp : Endpoint<SendOtpRequest, OtpResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required OtpService OtpService { get; init; }
    public required OtpNotificationService NotificationService { get; init; }

    public override void Configure()
    {
        Post("/api/auth/send-otp");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("OtpSend"));
        Description(d => d
            .WithTags("Authentication")
            .WithSummary("Send OTP via email or phone")
            .WithDescription("Generates and sends a 6-digit OTP code for verification."));
    }

    public override async Task HandleAsync(SendOtpRequest req, CancellationToken ct)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(req.ContactMethod))
        {
            ThrowError("Contact method is required");
            return;
        }

        if (req.ContactMethod.ToLower() != "email" && req.ContactMethod.ToLower() != "phone")
        {
            ThrowError("Contact method must be 'email' or 'phone'");
            return;
        }

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
        else
        {
            if (string.IsNullOrWhiteSpace(req.PhoneNumber))
            {
                ThrowError("Phone number is required when using phone contact method");
                return;
            }
            identifier = req.PhoneNumber;
        }

        // For login purpose, verify the user exists before sending OTP
        if (string.Equals(req.Purpose, "login", StringComparison.OrdinalIgnoreCase))
        {
            await using var session = DocumentStore.LightweightSession();

            User? user = req.ContactMethod.ToLower() switch
            {
                "email" => await session.Query<User>()
                    .FirstOrDefaultAsync(u => u.Email == identifier, token: ct),
                "phone" => await session.Query<User>()
                    .FirstOrDefaultAsync(u => u.PhoneNumber == identifier, token: ct),
                _ => null
            };

            if (user is null)
            {
                ThrowError("No account found with this contact information. Please register first.", 404);
                return;
            }
        }

        // Generate OTP (enforces resend cooldown)
        string otpCode;
        DateTime expiresAt;
        try
        {
            (otpCode, expiresAt) = await OtpService.GenerateOtpAsync(identifier, req.Purpose ?? "registration", ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 429);
            return;
        }

        // Send OTP via appropriate channel
        bool sent;
        if (req.ContactMethod.ToLower() == "email")
        {
            sent = await NotificationService.SendEmailOtpAsync(req.Email!, otpCode, req.Purpose ?? "registration");
        }
        else
        {
            sent = await NotificationService.SendSmsOtpAsync(req.PhoneNumber!, otpCode, req.Purpose ?? "registration");
        }

        if (!sent)
        {
            ThrowError($"Failed to send OTP to {req.ContactMethod}", 500);
            return;
        }

        Logger.LogInformation("OTP sent successfully via {ContactMethod}", req.ContactMethod);

        Response = new OtpResponse
        {
            Success = true,
            Message = $"OTP sent successfully to your {req.ContactMethod}",
            ExpiresAt = expiresAt,
            IsVerified = false
        };
    }
}
