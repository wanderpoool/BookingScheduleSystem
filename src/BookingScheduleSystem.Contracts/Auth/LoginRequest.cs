namespace BookingScheduleSystem.Contracts.Auth;

public sealed record LoginRequest
{
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public required string Password { get; init; }
}
