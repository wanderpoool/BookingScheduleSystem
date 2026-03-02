using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// Maps a user to a tenant with a specific role.
/// Cross-tenant document — does NOT use conjoined tenancy.
/// All queries must explicitly filter by TenantId and/or UserId.
/// </summary>
public sealed class UserTenant
{
    public UserTenantId Id { get; set; }
    public UserId UserId { get; set; }
    public TenantId TenantId { get; set; }
    public string Role { get; set; } = "Customer";
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
