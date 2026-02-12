namespace BookingScheduleSystem.Contracts.Auth;

public sealed record ResetPasswordRequest
{
    public required string Email { get; init; }
    public required string OtpVerificationToken { get; init; }
    public required string NewPassword { get; init; }
}
