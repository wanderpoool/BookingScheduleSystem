namespace BookingScheduleSystem.Contracts.Auth;

public sealed record VerifyOtpRequest
{
    /// <summary>
    /// Contact method: "email" or "phone"
    /// </summary>
    public required string ContactMethod { get; init; }

    /// <summary>
    /// Email address (if ContactMethod is "email")
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Phone number (if ContactMethod is "phone")
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// 6-digit OTP code
    /// </summary>
    public required string OtpCode { get; init; }

    /// <summary>
    /// Purpose of OTP: "registration", "login", "password-reset"
    /// </summary>
    public required string Purpose { get; init; }
}
