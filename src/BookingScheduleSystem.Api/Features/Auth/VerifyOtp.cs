using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Contracts.Auth;
using FastEndpoints;

namespace BookingScheduleSystem.Api.Features.Auth;

public sealed class VerifyOtp : Endpoint<VerifyOtpRequest, OtpResponse>
{
    public required OtpService OtpService { get; init; }

    public override void Configure()
    {
        Post("/api/auth/verify-otp");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("Auth"));
        Description(d => d
            .WithTags("Authentication")
            .WithSummary("Verify OTP code")
            .WithDescription("Verifies a 6-digit OTP code sent via email or phone."));
    }

    public override async Task HandleAsync(VerifyOtpRequest req, CancellationToken ct)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(req.ContactMethod))
        {
            ThrowError("Contact method is required");
            return;
        }

        if (string.IsNullOrWhiteSpace(req.OtpCode))
        {
            ThrowError("OTP code is required");
            return;
        }

        if (req.OtpCode.Length != 6 || !req.OtpCode.All(char.IsDigit))
        {
            ThrowError("OTP code must be a 6-digit number");
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

        // Verify OTP
        var (isValid, verificationToken) = await OtpService.VerifyOtpAsync(identifier, req.OtpCode, req.Purpose ?? "registration", ct);

        if (!isValid)
        {
            ThrowError("Invalid or expired OTP code. Please try again or request a new code.");
            return;
        }

        Logger.LogInformation("OTP verified successfully for identifier: {Identifier}", identifier);

        Response = new OtpResponse
        {
            Success = true,
            Message = "OTP verified successfully!",
            IsVerified = true,
            VerificationToken = verificationToken
        };
    }
}
