namespace BookingScheduleSystem.Contracts.Auth;

public sealed record PasswordSetupRequiredResponse
{
    public bool RequiresPasswordSetup { get; init; } = true;
    public required string ContactMethod { get; init; }
    public required string MaskedContact { get; init; }
}
