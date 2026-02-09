namespace BookingScheduleSystem.Contracts.Auth;

public sealed record OtpResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsVerified { get; init; }
}
