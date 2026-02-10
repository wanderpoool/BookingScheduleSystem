namespace BookingScheduleSystem.Contracts.Auth;

public sealed record LoginWithOtpRequest
{
    public required string ContactMethod { get; init; } // "email" or "phone"
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public required string OtpVerificationToken { get; init; }
}
