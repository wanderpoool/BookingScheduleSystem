using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// User document stored in Marten
/// </summary>
public sealed class User
{
    public UserId Id { get; set; }
    public required string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public TenantId? TenantId { get; set; }
    public bool IsGlobalAdmin { get; set; }
    public bool IsProvider { get; set; }
    public bool AutoAcceptBookings { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Provider working hours (must be within organization operating hours)
    /// JSON format: {"Monday": {"IsOpen": true, "OpenTime": "09:00", "CloseTime": "17:00"}, ...}
    /// </summary>
    public string? WorkingHours { get; set; }
}
