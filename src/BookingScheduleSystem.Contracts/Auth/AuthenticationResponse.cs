using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Auth;

public sealed record AuthenticationResponse
{
    public required string Token { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required UserId UserId { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public TenantId? TenantId { get; init; }
    public required bool IsGlobalAdmin { get; init; }
}
