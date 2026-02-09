using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Auth;

public sealed record RegisterUserRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public TenantId? TenantId { get; init; }
    public string? CreationCode { get; init; }

    /// <summary>
    /// Indicates if the user should be registered as a provider (business owner)
    /// Used when creating a new organization
    /// </summary>
    public bool IsProvider { get; init; }

    /// <summary>
    /// Provider working hours (only applicable for providers)
    /// JSON format: {"Monday": {"IsOpen": true, "OpenTime": "09:00", "CloseTime": "17:00"}, ...}
    /// Must be within organization operating hours
    /// </summary>
    public string? WorkingHours { get; init; }
}
