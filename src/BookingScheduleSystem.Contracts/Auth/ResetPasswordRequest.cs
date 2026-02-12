namespace BookingScheduleSystem.Contracts.Auth;

public sealed record ResetPasswordRequest
{
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string ContactMethod { get; init; } = "email";
    public required string OtpVerificationToken { get; init; }
    public required string NewPassword { get; init; }
}
