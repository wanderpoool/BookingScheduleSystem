using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Users;

public sealed record UserResponse
{
    public required UserId Id { get; init; }
    public required string Email { get; init; }
    public string? PhoneNumber { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public TenantId? TenantId { get; init; }
    public required bool IsGlobalAdmin { get; init; }
    public required bool IsProvider { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsActive { get; init; }
    public string? WorkingHours { get; init; }
    public bool AutoAcceptBookings { get; init; }
}
